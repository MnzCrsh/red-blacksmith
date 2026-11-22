import { sendIngestionStream } from './lib/grpc-ingestion.js';

/** Профиль пикового легитимного трафика с постепенным ростом и плато. */
export const options = {
  scenarios: {
    black_friday: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '20s', target: 5 },
        { duration: '30s', target: 15 },
        { duration: '1m', target: 15 },
        { duration: '20s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    ingestion_stream_success: ['rate>0.95'],
  },
};

/** Передаёт крупные последовательные batch-потоки, имитируя распродажу. */
export default async function () {
  await sendIngestionStream({
    eventCount: 80,
    interEventDelay: 0,
    streamPause: 0.15,
  });
}
