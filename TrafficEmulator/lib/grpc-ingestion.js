import { Counter, Rate, Trend } from 'k6/metrics';
import { sleep } from 'k6';
import { Client, Stream } from 'k6/net/grpc';
import { createPacketEvent } from './event-generator.js';
import { GRPC_ADDR, GRPC_CONNECT_TIMEOUT, INGEST_METHOD } from './config.js';

const client = new Client();
client.load([], 'proto/traffic-ingestion.proto');

const eventsSubmitted = new Counter('ingestion_events_submitted');
const streamsCompleted = new Counter('ingestion_streams_completed');
const streamFailures = new Counter('ingestion_stream_failures');
const streamSuccess = new Rate('ingestion_stream_success');
const streamDuration = new Trend('ingestion_stream_duration', true);

let connected = false;

/** Устанавливает одно gRPC-соединение на виртуального пользователя и переиспользует его между итерациями. */
function ensureConnected() {
  if (connected) {
    return;
  }

  client.connect(GRPC_ADDR, {
    plaintext: true,
    timeout: GRPC_CONNECT_TIMEOUT,
  });

  connected = true;
}

/** Отправляет один client-streaming RPC с указанным количеством PacketEvent. */
export async function sendIngestionStream({ eventCount, interEventDelay = 0, streamPause = 0 }) {
  const startedAt = Date.now();

  try {
    ensureConnected();

    const stream = new Stream(client, INGEST_METHOD);

    await new Promise((resolve, reject) => {
      let settled = false;

      stream.on('data', () => {
        // Сервер возвращает google.protobuf.Empty после завершения входящего потока.
      });

      stream.on('error', (error) => {
        if (settled) return;
        settled = true;
        reject(error);
      });

      stream.on('end', () => {
        if (settled) return;
        settled = true;
        resolve();
      });

      for (let index = 0; index < eventCount; index++) {
        stream.write(createPacketEvent());
        eventsSubmitted.add(1);

        if (interEventDelay > 0) {
          sleep(interEventDelay);
        }
      }

      stream.end();
    });

    streamsCompleted.add(1);
    streamSuccess.add(true);
    streamDuration.add(Date.now() - startedAt);

    if (streamPause > 0) {
      sleep(streamPause);
    }
  } catch (error) {
    streamFailures.add(1);
    streamSuccess.add(false);
    streamDuration.add(Date.now() - startedAt);
    console.error(`TrafficIngestion gRPC stream failed: ${String(error)}`);
    throw error;
  }
}
