
[CmdletBinding()]
param(
    [string]$Namespace = "redblacksmith",
    [string]$SecretsFile = (Join-Path $PSScriptRoot "..\secrets\local.env")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$requiredKeys = @(
    "POSTGRES_SUPERUSER_PASSWORD",
    "POSTGRES_PROCESSING_PASSWORD",
    "POSTGRES_API_PASSWORD",
    "CLICKHOUSE_PASSWORD",
    "CLICKHOUSE_OPERATOR_PASSWORD"
)

if (-not (Get-Command kubectl -ErrorAction SilentlyContinue)) {
    throw "kubectl не найден в PATH."
}

if (-not (Test-Path -LiteralPath $SecretsFile -PathType Leaf)) {
    throw "Файл секретов не найден: $SecretsFile"
}

$resolvedSecretsFile = (Resolve-Path -LiteralPath $SecretsFile).Path
$values = @{}

foreach ($line in [System.IO.File]::ReadAllLines($resolvedSecretsFile)) {
    $trimmedLine = $line.Trim()

    if ([string]::IsNullOrWhiteSpace($trimmedLine) -or $trimmedLine.StartsWith("#")) {
        continue
    }

    $separatorIndex = $line.IndexOf("=")

    if ($separatorIndex -lt 1) {
        throw "Некорректная строка в файле секретов."
    }

    $key = $line.Substring(0, $separatorIndex)
    $value = $line.Substring($separatorIndex + 1)

    if ($key -notmatch "^[A-Z][A-Z0-9_]*$") {
        throw "Некорректное имя переменной в файле секретов."
    }

    if ($values.ContainsKey($key)) {
        throw "Обнаружен повторяющийся ключ: $key"
    }

    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Пустое значение для ключа: $key"
    }

    if ($value -match "^(change-me|replace-me|<.+>)$") {
        throw "Для ключа $key не задано реальное значение."
    }

    if ($value.Length -lt 16) {
        throw "Значение $key должно содержать минимум 16 символов."
    }

    $values[$key] = $value
}

$missingKeys = @(
    $requiredKeys | Where-Object { -not $values.ContainsKey($_) }
)

if ($missingKeys.Count -gt 0) {
    throw "Отсутствуют обязательные ключи: $($missingKeys -join ', ')"
}

$unknownKeys = @(
    $values.Keys | Where-Object { $_ -notin $requiredKeys }
)

if ($unknownKeys.Count -gt 0) {
    throw "Неизвестные ключи: $($unknownKeys -join ', ')"
}

& kubectl get namespace $Namespace --output=name *> $null

if ($LASTEXITCODE -ne 0) {
    throw "Namespace '$Namespace' не найден. Сначала необходимо создать namespace."
}

$secretManifest = & kubectl create secret generic redblacksmith-secrets `
    --namespace $Namespace `
    "--from-env-file=$resolvedSecretsFile" `
    --dry-run=client `
    --output=yaml

if ($LASTEXITCODE -ne 0) {
    throw "Не удалось сформировать Kubernetes Secret."
}

if (-not $secretManifest) {
    throw "kubectl не сформировал манифест Secret."
}

$secretManifest | & kubectl apply `
    --server-side `
    --field-manager=redblacksmith-secrets `
    --filename=-

if ($LASTEXITCODE -ne 0) {
    throw "Не удалось применить Kubernetes Secret."
}

Write-Host "Secret 'redblacksmith-secrets' успешно применён в namespace '$Namespace'."
