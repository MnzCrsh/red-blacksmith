# red-blacksmith repository overview
Microservice-oriented network traffic monitoring system inspired by netstat. Provides real-time traffic inspection

## architecture / system design diagram
```mermaid
flowchart TD
    EM[Traffic Emulator Service]
    CS[ChannelService ETL ingestion]
    W[Worker Processing Service]
    CH[ClickHouse EventStore]
    PG[PostgreSQL Redis]
    API[Backend API Service]
    FE[Frontend Visualization]
    AUTH[Auth Service OAuth2]
    MM[Prometheus Grafana]

    EM -->|push/gRPC PacketEvent| CS
    CS -->|Kafka Topic| W
    W -->|Write Aggregates| CH
    W -->|Update Projections| PG
    FE -->|OAuth2| AUTH
    FE -->|HTTP REST| API
    API -->|Read Projections| PG
    W -->|Expose Metrics| MM
    CS -->|Expose Metrics| MM
