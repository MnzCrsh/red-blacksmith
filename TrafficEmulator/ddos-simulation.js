import { sendIngestionStream } from './lib/grpc-ingestion.js';

/** Ограниченная резкая нагрузка только для собственного локального стенда. */
export const options = {
  scenarios: {
    ddos_simulation: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '3s', target: 10 },
        { duration: '20s', target: 10 },
        { duration: '5s', target: 0 },
      ],
      gracefulRampDown: '5s',
    },
  },
  thresholds: {
    ingestion_stream_success: ['rate>0.80'],
  },
};

/** Создаёт резкие всплески событий с ограничением в десять виртуальных пользователей. */
export default async function () {
  await sendIngestionStream({
    eventCount: 120,
    interEventDelay: 0,
    streamPause: 0,
  });
}
