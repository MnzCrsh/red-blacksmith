@{
    KindNodeImage = "kindest/node:v1.35.0@sha256:452d707d4862f52530247495d180205e029056831160e22870e37e3f6c1ac31f"

    PostgreSql = @{
        ReleaseName = "postgresql"
        Chart = "bitnami/postgresql"
        RepositoryName = "bitnami"
        RepositoryUrl = "https://charts.bitnami.com/bitnami"
        Version = "18.12.23"
        ValuesFile = "Infrastructure/helm/values/postgresql.yaml"
    }

    ClickHouseOperator = @{
        ReleaseName = "clickhouse-operator"
        Chart = "altinity/altinity-clickhouse-operator"
        RepositoryName = "altinity"
        RepositoryUrl = "https://helm.altinity.com"
        Version = "0.27.4"
        ValuesFile = "Infrastructure/helm/values/altinity-clickhouse-operator.yaml"
    }

    StrimziOperator = @{
        ReleaseName = "strimzi-kafka-operator"
        Chart = "oci://quay.io/strimzi-helm/strimzi-kafka-operator"
        Version = "1.3.1"
        ValuesFile = "Infrastructure/helm/values/strimzi-kafka-operator.yaml"
    }

    ClickHouse = @{
        Version = "26.8"
        Image = "clickhouse/clickhouse-server:26.8"
    }

    Kafka = @{
        Version = "4.3.2"
    }

    Traefik = @{
        ReleaseName = "traefik"
        Chart = "traefik/traefik"
        RepositoryName = "traefik"
        RepositoryUrl = "https://traefik.github.io/charts"
        Version = "42.4.1"
        Namespace = "traefik-system"
        ValuesFile = "Infrastructure/helm/values/traefik.yaml"
    }

    PrometheusStack = @{
        ReleaseName = "monitoring"
        Chart = "prometheus-community/kube-prometheus-stack"
        RepositoryName = "prometheus-community"
        RepositoryUrl = "https://prometheus-community.github.io/helm-charts"
        Version = "93.6.1"
        Namespace = "observability"
        ValuesFile = "Infrastructure/helm/values/kube-prometheus-stack.yaml"
    }

    OpenTelemetryCollector = @{
        ReleaseName = "otel-collector"
        Chart = "open-telemetry/opentelemetry-collector"
        RepositoryName = "open-telemetry"
        RepositoryUrl = "https://open-telemetry.github.io/opentelemetry-helm-charts"
        Version = "0.180.0"
        Namespace = "observability"
        ValuesFile = "Infrastructure/helm/values/opentelemetry-collector.yaml"
    }

    Tempo = @{
        ReleaseName = "tempo"
        Chart = "grafana-community/tempo"
        RepositoryName = "grafana-community"
        RepositoryUrl = "https://grafana-community.github.io/helm-charts"
        Version = "3.2.1"
        Namespace = "observability"
        ValuesFile = "Infrastructure/helm/values/tempo.yaml"
    }
}
