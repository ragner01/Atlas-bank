import http from 'k6/http';
import { check, sleep } from 'k6';
export const options = {
  scenarios: {
    spike: {
      executor: 'ramping-arrival-rate',
      startRate: 1000,
      timeUnit: '1s',
      preAllocatedVUs: 2000,
      stages: [
        { duration: '30s', target: 3000 },
        { duration: '1m',  target: 5000 },
        { duration: '2m',  target: 5000 },
        { duration: '30s', target: 0 }
      ]
    }
  },
  thresholds: {
    http_req_duration: ['p(99)<120'],
    http_req_failed:    ['rate<0.005']
  }
};
const base = __ENV.BASE_URL || 'http://localhost:5191';
export default function () {
  const key = crypto.randomUUID();
  const payload = JSON.stringify({
    SourceAccountId: 'acc_spike_src',
    DestinationAccountId: 'acc_spike_dst',
    Minor: 1000,
    Currency: 'NGN',
    Narration: 'perf'
  });
  const res = http.post(`${base}/payments/transfers/fast`, payload, {
    headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key }
  });
  check(res, { '202/200': r => r.status === 202 || r.status === 200 });
  // no sleep; we're load-driving
}
