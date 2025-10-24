import http from 'k6/http';
import { check } from 'k6';
export const options = {
  scenarios: {
    reads: {
      executor: 'ramping-arrival-rate',
      startRate: 2000, timeUnit: '1s', preAllocatedVUs: 4000,
      stages: [
        { duration: '30s', target: 5000 },
        { duration: '1m',  target: 10000 },
        { duration: '1m',  target: 10000 },
        { duration: '30s', target: 0 }
      ]
    }
  },
  thresholds: {
    http_req_duration: ['p(99)<50'], // cache hits should be blazing
    http_req_failed: ['rate<0.005']
  }
};
const base = __ENV.BASE_URL || 'http://localhost:5181';
export default function () {
  const res = http.get(`${base}/ledger/accounts/acc_hedged_dst/balance?currency=NGN`);
  check(res, { '200': r => r.status === 200 });
}
