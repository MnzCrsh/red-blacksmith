CREATE DATABASE IF NOT EXISTS traffic;

CREATE TABLE IF NOT EXISTS {{database}}.packet_events
(
    event_id UUID COMMENT 'Идентификатор события',
    batch_id UUID COMMENT 'Идентификатор группы',
    source_id UUID COMMENT 'Идентификатор события из источника',
    occurred_at_utc DateTime64(7, 'UTC') COMMENT 'Время возникновения события',
    source_ip IPv6 COMMENT 'IP от которого поступило сетевое событие',
    destination_ip IPv6 COMMENT 'IP куда направлялось сетевое событие',
    source_port Nullable(UInt16) COMMENT 'Порт через который поступило сетевое событие',
    destination_port Nullable(UInt16) COMMENT 'Порт в который направлялось сетевое событие',
    protocol UInt8 COMMENT 'Протокол использовавшийся для сетевого события',
    packet_length_bytes UInt32 COMMENT 'Размер пакета сетевого события',
    ingested_at_utc DateTime64(3, 'UTC') DEFAULT now64(3, 'UTC') COMMENT 'Время обработка Ingestion сервисом'
)
    ENGINE = MergeTree
        PARTITION BY toYYYYMM(occurred_at_utc)
        ORDER BY (occurred_at_utc, source_id, event_id)
        TTL occurred_at_utc + INTERVAL 30 DAY DELETE
        SETTINGS non_replicated_deduplication_window = 1000,
                 index_granularity = 8192,
                 ttl_only_drop_parts = 1;
