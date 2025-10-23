import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
export let errorRate = new Rate('errors');
export let ledgerThroughput = new Rate('ledger_throughput');

// Stress test configuration
export let options = {
  stages: [
    { duration: '1m', target: 50 },   // Ramp up to 50 users
    { duration: '2m', target: 100 },  // Ramp up to 100 users
    { duration: '2m', target: 200 },  // Ramp up to 200 users
    { duration: '2m', target: 500 }, // Ramp up to 500 users
    { duration: '2m', target: 1000 }, // Ramp up to 1000 users
    { duration: '5m', target: 1000 }, // Stay at 1000 users
    { duration: '2m', target: 0 },    // Ramp down to 0 users
  ],
  thresholds: {
    http_req_duration: ['p(99)<500'], // 99% of requests must complete below 500ms
    http_req_failed: ['rate<0.05'],   // Error rate must be below 5%
    errors: ['rate<0.05'],            // Custom error rate must be below 5%
    ledger_throughput: ['rate>0.8'],  // Ledger throughput should be > 80%
  },
};

// Test data
const BASE_URL = 'http://localhost:5000';
const TENANT_ID = 'stress-test-tenant';
const ACCOUNT_ID_1 = 'acc_stress_001';
const ACCOUNT_ID_2 = 'acc_stress_002';

// Authentication token
const AUTH_TOKEN = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...';

export default function() {
  const headers = {
    'Authorization': `Bearer ${AUTH_TOKEN}`,
    'X-Tenant-Id': TENANT_ID,
    'Content-Type': 'application/json',
  };

  // Random test selection to simulate realistic load
  const testType = Math.random();
  
  if (testType < 0.4) {
    // 40% - Ledger operations (most critical)
    testLedgerOperations(headers);
  } else if (testType < 0.7) {
    // 30% - Payment transfers
    testPaymentTransfers(headers);
  } else if (testType < 0.9) {
    // 20% - Read operations
    testReadOperations(headers);
  } else {
    // 10% - Health checks
    testHealthChecks(headers);
  }

  sleep(Math.random() * 0.1); // Random sleep between 0-100ms
}

function testLedgerOperations(headers) {
  // Test journal entry creation
  const journalEntryPayload = JSON.stringify({
    reference: `STRESS-${__VU}-${__ITER}-${Date.now()}`,
    description: 'Stress test journal entry',
    entryDate: new Date().toISOString(),
    lines: [
      {
        accountId: ACCOUNT_ID_1,
        amount: { value: 1000, currency: 'NGN', scale: 2 },
        type: 'Debit'
      },
      {
        accountId: ACCOUNT_ID_2,
        amount: { value: 1000, currency: 'NGN', scale: 2 },
        type: 'Credit'
      }
    ]
  });

  const response = http.post(`${BASE_URL}/api/journal-entries`, journalEntryPayload, { headers });
  
  const success = check(response, {
    'ledger operation status is 200': (r) => r.status === 200,
    'ledger operation response time < 500ms': (r) => r.timings.duration < 500,
    'ledger operation has valid JSON': (r) => r.json('isSuccess') !== undefined,
  });
  
  errorRate.add(!success);
  ledgerThroughput.add(success);
}

function testPaymentTransfers(headers) {
  // Test payment transfer
  const transferPayload = JSON.stringify({
    sourceAccountId: ACCOUNT_ID_1,
    destinationAccountId: ACCOUNT_ID_2,
    amount: {
      value: Math.floor(Math.random() * 50000) + 1000, // Random amount between 1000-51000
      currency: 'NGN',
      scale: 2
    },
    reference: `STRESS-TRANSFER-${__VU}-${__ITER}`,
    description: 'Stress test transfer'
  });

  const response = http.post(`${BASE_URL}/api/transfers`, transferPayload, { 
    headers: {
      ...headers,
      'Idempotency-Key': `stress-${__VU}-${__ITER}-${Date.now()}`
    }
  });
  
  const success = check(response, {
    'transfer status is 200 or 202': (r) => r.status === 200 || r.status === 202,
    'transfer response time < 500ms': (r) => r.timings.duration < 500,
    'transfer has valid JSON': (r) => r.json('isSuccess') !== undefined,
  });
  
  errorRate.add(!success);
}

function testReadOperations(headers) {
  // Test account balance query
  const response = http.get(`${BASE_URL}/api/accounts/${ACCOUNT_ID_1}/balance`, { headers });
  
  const success = check(response, {
    'read operation status is 200': (r) => r.status === 200,
    'read operation response time < 200ms': (r) => r.timings.duration < 200,
    'read operation has valid JSON': (r) => r.json('accountId') !== undefined,
  });
  
  errorRate.add(!success);
}

function testHealthChecks(headers) {
  // Test health check
  const response = http.get(`${BASE_URL}/health`, { headers });
  
  const success = check(response, {
    'health check status is 200': (r) => r.status === 200,
    'health check response time < 100ms': (r) => r.timings.duration < 100,
  });
  
  errorRate.add(!success);
}

export function setup() {
  console.log('Setting up stress test...');
  
  // Verify services are running
  const healthResponse = http.get(`${BASE_URL}/health`);
  if (healthResponse.status !== 200) {
    throw new Error('Services are not healthy');
  }
  
  // Create test accounts if they don't exist
  const headers = {
    'Authorization': `Bearer ${AUTH_TOKEN}`,
    'X-Tenant-Id': TENANT_ID,
    'Content-Type': 'application/json',
  };
  
  // Create account 1
  const account1Payload = JSON.stringify({
    accountNumber: ACCOUNT_ID_1,
    name: 'Stress Test Account 1',
    type: 'Asset',
    currency: 'NGN'
  });
  
  const account1Response = http.post(`${BASE_URL}/api/accounts`, account1Payload, { headers });
  if (account1Response.status === 200 || account1Response.status === 409) { // 409 = already exists
    console.log('Test account 1 ready');
  }
  
  // Create account 2
  const account2Payload = JSON.stringify({
    accountNumber: ACCOUNT_ID_2,
    name: 'Stress Test Account 2',
    type: 'Liability',
    currency: 'NGN'
  });
  
  const account2Response = http.post(`${BASE_URL}/api/accounts`, account2Payload, { headers });
  if (account2Response.status === 200 || account2Response.status === 409) { // 409 = already exists
    console.log('Test account 2 ready');
  }
  
  console.log('Stress test setup complete, starting test...');
  return { startTime: Date.now() };
}

export function teardown(data) {
  const duration = Date.now() - data.startTime;
  console.log(`Stress test completed in ${duration}ms`);
  console.log('Stress test results:');
  console.log('- Peak load: 1000 concurrent users');
  console.log('- Test duration: ~15 minutes');
  console.log('- Target: 99% requests < 500ms, < 5% error rate');
}
