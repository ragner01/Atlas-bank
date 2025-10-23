import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
export let errorRate = new Rate('errors');

// Test configuration
export let options = {
  stages: [
    { duration: '2m', target: 100 }, // Ramp up to 100 users
    { duration: '5m', target: 100 }, // Stay at 100 users
    { duration: '2m', target: 200 }, // Ramp up to 200 users
    { duration: '5m', target: 200 }, // Stay at 200 users
    { duration: '2m', target: 0 },   // Ramp down to 0 users
  ],
  thresholds: {
    http_req_duration: ['p(95)<150'], // 95% of requests must complete below 150ms
    http_req_failed: ['rate<0.01'],   // Error rate must be below 1%
    errors: ['rate<0.01'],            // Custom error rate must be below 1%
  },
};

// Test data
const BASE_URL = 'http://localhost:5000';
const TENANT_ID = 'test-tenant-1';
const ACCOUNT_ID_1 = 'acc_1234567890';
const ACCOUNT_ID_2 = 'acc_0987654321';

// Authentication token (in real scenario, this would be obtained via login)
const AUTH_TOKEN = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...';

export default function() {
  const headers = {
    'Authorization': `Bearer ${AUTH_TOKEN}`,
    'X-Tenant-Id': TENANT_ID,
    'Content-Type': 'application/json',
  };

  // Test 1: Get account balance
  let response = http.get(`${BASE_URL}/api/accounts/${ACCOUNT_ID_1}/balance`, { headers });
  let success = check(response, {
    'account balance status is 200': (r) => r.status === 200,
    'account balance response time < 150ms': (r) => r.timings.duration < 150,
    'account balance has valid JSON': (r) => r.json('accountId') !== undefined,
  });
  errorRate.add(!success);

  sleep(0.1);

  // Test 2: Create payment transfer
  const transferPayload = JSON.stringify({
    sourceAccountId: ACCOUNT_ID_1,
    destinationAccountId: ACCOUNT_ID_2,
    amount: {
      value: Math.floor(Math.random() * 10000) + 1000, // Random amount between 1000-11000
      currency: 'NGN',
      scale: 2
    },
    reference: `Test transfer ${__VU}-${__ITER}`,
    description: 'Performance test transfer'
  });

  response = http.post(`${BASE_URL}/api/transfers`, transferPayload, { 
    headers: {
      ...headers,
      'Idempotency-Key': `test-${__VU}-${__ITER}-${Date.now()}`
    }
  });
  
  success = check(response, {
    'transfer creation status is 200 or 202': (r) => r.status === 200 || r.status === 202,
    'transfer creation response time < 150ms': (r) => r.timings.duration < 150,
    'transfer creation has valid JSON': (r) => r.json('isSuccess') !== undefined,
  });
  errorRate.add(!success);

  sleep(0.1);

  // Test 3: Get journal entries
  response = http.get(`${BASE_URL}/api/journal-entries`, { headers });
  success = check(response, {
    'journal entries status is 200': (r) => r.status === 200,
    'journal entries response time < 150ms': (r) => r.timings.duration < 150,
    'journal entries has valid JSON': (r) => Array.isArray(r.json()),
  });
  errorRate.add(!success);

  sleep(0.1);

  // Test 4: Health check
  response = http.get(`${BASE_URL}/health`, { headers });
  success = check(response, {
    'health check status is 200': (r) => r.status === 200,
    'health check response time < 50ms': (r) => r.timings.duration < 50,
  });
  errorRate.add(!success);

  sleep(0.1);
}

export function setup() {
  // Setup function - runs once before the test
  console.log('Setting up performance test...');
  
  // Verify services are running
  const healthResponse = http.get(`${BASE_URL}/health`);
  if (healthResponse.status !== 200) {
    throw new Error('Services are not healthy');
  }
  
  console.log('Services are healthy, starting performance test...');
  return { startTime: Date.now() };
}

export function teardown(data) {
  // Teardown function - runs once after the test
  const duration = Date.now() - data.startTime;
  console.log(`Performance test completed in ${duration}ms`);
}
