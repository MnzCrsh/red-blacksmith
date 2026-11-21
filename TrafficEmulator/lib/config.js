/** Адрес gRPC-сервиса TrafficIngestion. */
export const GRPC_ADDR = __ENV.GRPC_ADDR || 'traffic-ingestion:8080';

/** Полное имя client-streaming RPC из .proto. */
export const INGEST_METHOD = 'redblacksmith.trafficingestion.v1.TrafficIngestion/Ingest';

/** Таймаут установки соединения с gRPC-сервисом. */
export const GRPC_CONNECT_TIMEOUT = __ENV.GRPC_CONNECT_TIMEOUT || '10s';
