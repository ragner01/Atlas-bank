import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');

export const options = {
  stages: [
    { duration: '2m', target: 10 }, // Ramp up to 10 users
    { duration: '5m', target: 10 }, // Stay at 10 users
    { duration: '2m', target: 20 }, // Ramp up to 20 users
    { duration: '5m', target: 20 }, // Stay at 20 users
    { duration: '2m', target: 0 },  // Ramp down to 0 users
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests must complete below 500ms
    http_req_failed: ['rate<0.1'],    // Error rate must be below 10%
    errors: ['rate<0.1'],              // Custom error rate must be below 10%
  },
};

const BASE_URL = 'http://localhost:5000';
const TENANT_ID = 'tnt_demo';

export default function() {
  // Test API Gateway health
  const healthResponse = http.get(`${BASE_URL}/health`);
  check(healthResponse, {
    'Gateway health check': (r) => r.status === 200,
  });
  errorRate.add(healthResponse.status !== 200);

  // Test Ledger API through Gateway
  const ledgerResponse = http.get(`${BASE_URL}/api/accounts/test-account`, {
    headers: {
      'X-Tenant-Id': TENANT_ID,
    },
  });
  check(ledgerResponse, {
    'Ledger API response': (r) => r.status === 200,
    'Ledger API response time': (r) => r.timings.duration < 1000,
  });
  errorRate.add(ledgerResponse.status !== 200);

  // Test Payments API through Gateway
  const transferPayload = JSON.stringify({
    amount: Math.random() * 1000,
    fromAccount: 'test-from',
    toAccount: 'test-to',
    reference: `test-transfer-${Date.now()}`,
  });

  const paymentsResponse = http.post(`${BASE_URL}/api/transfers`, transferPayload, {
    headers: {
      'Content-Type': 'application/json',
      'X-Tenant-Id': TENANT_ID,
      'Idempotency-Key': `test-key-${Date.now()}`,
    },
  });
  check(paymentsResponse, {
    'Payments API response': (r) => r.status === 200,
    'Payments API response time': (r) => r.timings.duration < 2000,
  });
  errorRate.add(paymentsResponse.status !== 200);

  sleep(1);
}

export function handleSummary(data) {
  return {
    'performance-test-results.json': JSON.stringify(data, null, 2),
  };
}
