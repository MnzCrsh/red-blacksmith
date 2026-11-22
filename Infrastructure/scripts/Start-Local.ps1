
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory)]
    [string]$SecretsFile,

    [string]$ClusterName = "redblacksmith"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)]
        [string]$Command,

        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$Operation
    )

    & $Command @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$Operation завершилась с кодом $LASTEXITCODE."
    }
}

function Resolve-RepositoryPath {
    param([Parameter(Mandatory)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $script:Root $Path
}

function Find-ProjectDockerfile {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectDirectory
    )

    $rootDockerfile = Join-Path $ProjectDirectory "Dockerfile"

    if (Test-Path -LiteralPath $rootDockerfile -PathType Leaf) {
        return $rootDockerfile
    }

    $candidates = @(
        Get-ChildItem -LiteralPath $ProjectDirectory -Filter "Dockerfile" -File -Recurse |
            Where-Object {
                $_.FullName -notmatch '[\\/](bin|obj|node_modules|\.git)[\\/]'
            }
    )

    if ($candidates.Count -eq 0) {
        throw "Dockerfile не найден в '$ProjectDirectory'."
    }

    if ($candidates.Count -gt 1) {
        $paths = $candidates.FullName -join [Environment]::NewLine
        throw "Найдено несколько Dockerfile в '$ProjectDirectory':$([Environment]::NewLine)$paths"
    }

    return $candidates[0].FullName
}

function Find-SecretsScript {
    $candidates = @(
        Get-ChildItem -LiteralPath $script:Root -Filter "*.ps1" -File -Recurse |
            Where-Object {
                $_.FullName -ne $PSCommandPath -and
                $_.FullName -notmatch '[\\/](\.git|node_modules|bin|obj)[\\/]'
            } |
            Where-Object {
                $content = [System.IO.File]::ReadAllText($_.FullName)

                $content -match 'kubectl\s+create\s+secret\s+generic\s+redblacksmith-secrets' -and
                $content -match 'POSTGRES_SUPERUSER_PASSWORD' -and
                $content -match 'SecretsFile'
            }
    )

    if ($candidates.Count -eq 0) {
        throw "Не найден существующий PowerShell-скрипт создания redblacksmith-secrets."
    }

    if ($candidates.Count -gt 1) {
        $paths = $candidates.FullName -join [Environment]::NewLine
        throw "Найдено несколько скриптов секретов. Невозможно однозначно выбрать:$([Environment]::NewLine)$paths"
    }

    return $candidates[0].FullName
}

function Wait-ForCrd {
    param([Parameter(Mandatory)][string]$Name)

    Invoke-NativeCommand kubectl @(
        "wait",
        "--for=condition=Established",
        "--timeout=180s",
        "crd/$Name"
    ) "Регистрация CRD '$Name'"
}


if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
    throw "Корень репозитория не найден: $RepositoryRoot"
}

$script:Root = (Resolve-Path -LiteralPath $RepositoryRoot).Path

$resolvedSecretsFile = Resolve-RepositoryPath $SecretsFile

if (-not (Test-Path -LiteralPath $resolvedSecretsFile -PathType Leaf)) {
    throw "Файл секретов не найден: $resolvedSecretsFile"
}

$resolvedSecretsFile = (Resolve-Path -LiteralPath $resolvedSecretsFile).Path

$infrastructureRoot = Join-Path $script:Root "Infrastructure"

$kindConfig = Join-Path $infrastructureRoot "kind/cluster.yaml"
$releasesFile = Join-Path $infrastructureRoot "helm/releases.psd1"
$namespaceFile = Join-Path $infrastructureRoot "kubernetes/base/namespace.yaml"

$clickHouseComponent = Join-Path $infrastructureRoot "kubernetes/components/clickhouse"
$kafkaComponent = Join-Path $infrastructureRoot "kubernetes/components/kafka"
$kindOverlay = Join-Path $infrastructureRoot "kubernetes/overlays/kind"
$observabilityDirectory = Join-Path $infrastructureRoot "kubernetes/observability"

$appConfigFile = Join-Path $infrastructureRoot "kubernetes/base/config/app-config.yaml"

foreach ($requiredFile in @($kindConfig, $releasesFile, $namespaceFile)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Не найден обязательный файл: $requiredFile"
    }
}

foreach ($requiredDirectory in @(
    $clickHouseComponent,
    $kafkaComponent,
    $kindOverlay,
    $observabilityDirectory
)) {
    if (-not (Test-Path -LiteralPath $requiredDirectory -PathType Container)) {
        throw "Не найдена директория инфраструктуры: $requiredDirectory"
    }

    if (-not (Test-Path -LiteralPath (Join-Path $requiredDirectory "kustomization.yaml") -PathType Leaf)) {
        throw "В директории отсутствует kustomization.yaml: $requiredDirectory"
    }
}

$requiredCommands = @("docker", "kind", "kubectl", "helm")

foreach ($command in $requiredCommands) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Не найдена необходимая команда: $command"
    }
}

$releaseConfig = Import-PowerShellDataFile -LiteralPath $releasesFile

if (-not $releaseConfig.ContainsKey("KindNodeImage")) {
    throw "В releases.psd1 отсутствует KindNodeImage."
}

$buildTargets = @(
    @{
        Name = "traffic-ingestion"
        Directory = "TrafficIngestion"
    },
    @{
        Name = "traffic-processing"
        Directory = "TrafficProcessing"
    },
    @{
        Name = "traffic-api"
        Directory = "TrafficApi"
    },
    @{
        Name = "traffic-dashboard"
        Directory = "TrafficDashboard"
    },
    @{
        Name = "traffic-emulator"
        Directory = "TrafficEmulator"
    }
)

Write-Host "`n=== Docker images ===" -ForegroundColor Cyan

$images = @()

foreach ($target in $buildTargets) {
    $projectDirectory = Join-Path $script:Root $target.Directory

    if (-not (Test-Path -LiteralPath $projectDirectory -PathType Container)) {
        throw "Проект '$($target.Name)' не найден: $projectDirectory"
    }

    $dockerfile = Find-ProjectDockerfile $projectDirectory
    $image = "redblacksmith/$($target.Name):local"

    Write-Host "`nBuilding $image" -ForegroundColor Yellow
    Write-Host "Dockerfile: $dockerfile"
    Write-Host "Context:    $projectDirectory"

    Invoke-NativeCommand docker @(
        "build",
        "--tag", $image,
        "--file", $dockerfile,
        $projectDirectory
    ) "Сборка $image"

    $images += $image
}

Write-Host "`n=== Kind cluster ===" -ForegroundColor Cyan

$clusters = @(& kind get clusters)

if ($LASTEXITCODE -ne 0) {
    throw "Не удалось получить список Kind-кластеров."
}

if ($clusters -notcontains $ClusterName) {
    $createArguments = @(
        "create",
        "cluster",
        "--name", $ClusterName,
        "--config", $kindConfig,
        "--image", $releaseConfig.KindNodeImage,
        "--wait", "5m"
    )

    Invoke-NativeCommand kind $createArguments "Создание Kind-кластера"
}
else {
    Write-Host "Кластер '$ClusterName' уже существует. Используем его."
}

$context = "kind-$ClusterName"

Invoke-NativeCommand kubectl @(
    "config",
    "use-context",
    $context
) "Выбор Kubernetes context"

Write-Host "`n=== Loading images into Kind ===" -ForegroundColor Cyan

foreach ($image in $images) {
    Invoke-NativeCommand kind @(
        "load",
        "docker-image",
        $image,
        "--name",
        $ClusterName
    ) "Загрузка $image в Kind"
}

Write-Host "`n=== Namespace ===" -ForegroundColor Cyan

Invoke-NativeCommand kubectl @(
    "apply",
    "--filename",
    $namespaceFile
) "Создание Namespace"

Write-Host "`n=== App ConfigMap ===" -ForegroundColor Cyan

if (-not (Test-Path -LiteralPath $appConfigFile -PathType Leaf)) {
    throw "Не найден файл конфигурации: $appConfigFile"
}

Invoke-NativeCommand kubectl @(
    "apply",
    "--namespace", "redblacksmith",
    "--filename", $appConfigFile
) "Применение ConfigMap redblacksmith-config"

Write-Host "`n=== Kubernetes Secrets ===" -ForegroundColor Cyan

$secretsScript = Find-SecretsScript

Write-Host "Используется существующий скрипт: $secretsScript"

& $secretsScript `
    -Namespace "redblacksmith" `
    -SecretsFile $resolvedSecretsFile

if (-not $?) {
    throw "Скрипт создания Kubernetes Secrets завершился с ошибкой."
}

Write-Host "`n=== Helm repositories ===" -ForegroundColor Cyan

$chartKeys = @(
    "PostgreSql",
    "ClickHouseOperator",
    "StrimziOperator",
    "Traefik",
    "PrometheusStack",
    "Tempo",
    "OpenTelemetryCollector"
)

$chartEntries = @()

foreach ($key in $chartKeys) {
    if (-not $releaseConfig.ContainsKey($key)) {
        continue
    }

    $release = $releaseConfig[$key]

    if ($release -is [System.Collections.IDictionary] -and $release.Contains("Chart")) {
        $chartEntries += [PSCustomObject]@{
            Key = $key
            Data = $release
        }
    }
}

foreach ($key in $releaseConfig.Keys) {
    if ($key -in $chartKeys) {
        continue
    }

    $release = $releaseConfig[$key]

    if ($release -is [System.Collections.IDictionary] -and $release.Contains("Chart")) {
        $chartEntries += [PSCustomObject]@{
            Key = $key
            Data = $release
        }
    }
}

$chartText = ($chartEntries | ForEach-Object { $_.Data.Chart }) -join "`n"

foreach ($requiredChart in @(
    "kube-prometheus-stack",
    "opentelemetry-collector",
    "tempo"
)) {
    if ($chartText -notmatch [regex]::Escape($requiredChart)) {
        throw "В releases.psd1 отсутствует Helm chart '$requiredChart'."
    }
}

$repositories = @{}

foreach ($entry in $chartEntries) {
    $release = $entry.Data

    if (
        $release.Contains("RepositoryName") -and
        $release.Contains("RepositoryUrl")
    ) {
        $repositories[$release.RepositoryName] = $release.RepositoryUrl
    }
}

foreach ($repositoryName in $repositories.Keys) {
    Invoke-NativeCommand helm @(
        "repo",
        "add",
        $repositoryName,
        $repositories[$repositoryName],
        "--force-update"
    ) "Добавление Helm repository '$repositoryName'"
}

if ($repositories.Count -gt 0) {
    Invoke-NativeCommand helm @("repo", "update") "Обновление Helm repositories"
}

Write-Host "`n=== Installing Helm releases ===" -ForegroundColor Cyan

foreach ($entry in $chartEntries) {
    $release = $entry.Data

    if (-not $release.Contains("ReleaseName")) {
        throw "У Helm-релиза '$($entry.Key)' отсутствует ReleaseName."
    }

    $namespace = if ($release.Contains("Namespace")) {
        $release.Namespace
    }
    else {
        "redblacksmith"
    }

    $arguments = @(
        "upgrade",
        "--install",
        $release.ReleaseName,
        $release.Chart,
        "--namespace",
        $namespace,
        "--create-namespace"
    )

    if ($release.Contains("Version")) {
        $arguments += @("--version", $release.Version)
    }

    if ($release.Contains("ValuesFile")) {
        $valuesFile = Resolve-RepositoryPath $release.ValuesFile

        if (-not (Test-Path -LiteralPath $valuesFile -PathType Leaf)) {
            throw "Values-файл не найден: $valuesFile"
        }

        $arguments += @("--values", $valuesFile)
    }

    Write-Host "`nInstalling $($release.ReleaseName) into $namespace"

    Invoke-NativeCommand helm $arguments "Установка Helm-релиза '$($release.ReleaseName)'"
}

Write-Host "`n=== Custom Resource Definitions ===" -ForegroundColor Cyan

Wait-ForCrd "clickhouseinstallations.clickhouse.altinity.com"
Wait-ForCrd "kafkas.kafka.strimzi.io"
Wait-ForCrd "kafkanodepools.kafka.strimzi.io"
Wait-ForCrd "servicemonitors.monitoring.coreos.com"
Wait-ForCrd "prometheusrules.monitoring.coreos.com"

Write-Host "`n=== Infrastructure resources ===" -ForegroundColor Cyan

Invoke-NativeCommand kubectl @(
    "apply",
    "-k",
    $clickHouseComponent
) "Применение ClickHouse"

Invoke-NativeCommand kubectl @(
    "apply",
    "-k",
    $kafkaComponent
) "Применение Kafka"

Write-Host "`n=== Observability ===" -ForegroundColor Cyan

Invoke-NativeCommand kubectl @(
    "apply",
    "-k",
    $observabilityDirectory
) "Применение observability"

Write-Host "`n=== Applications ===" -ForegroundColor Cyan

Invoke-NativeCommand kubectl @(
    "apply",
    "-k",
    $kindOverlay
) "Применение приложений и Ingress"

Write-Host "`n=== Opening TrafficDashboard ===" -ForegroundColor Cyan

Start-Process "http://localhost:8080"

Write-Host "`nЛокальная инфраструктура передана Kubernetes." -ForegroundColor Green
Write-Host "Dashboard: http://localhost:8080"
 