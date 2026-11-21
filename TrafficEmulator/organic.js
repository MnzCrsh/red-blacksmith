import { sendIngestionStream } from './lib/grpc-ingestion.js';

/** Плавный профиль обычного сетевого трафика. */
export const options = {
  scenarios: {
    organic_traffic: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '20s', target: 3 },
        { duration: '2m', target: 3 },
        { duration: '20s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    ingestion_stream_success: ['rate>0.98'],
  },
};

/** Выполняет одну порцию органического трафика от имени виртуального источника. */
export default async function () {
  await sendIngestionStream({
    eventCount: 12,
    interEventDelay: 0.05 + Math.random() * 0.1,
    streamPause: 0.2 + Math.random() * 0.3,
  });
}
