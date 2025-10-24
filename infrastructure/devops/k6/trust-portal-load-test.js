import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');

export const options = {
  stages: [
    { duration: '2m', target: 10 }, // Ramp up to 10 users
    { duration: '5m', target: 10 }, // Stay at 10 users
    { duration: '2m', target: 50 }, // Ramp up to 50 users
    { duration: '5m', target: 50 }, // Stay at 50 users
    { duration: '2m', target: 100 }, // Ramp up to 100 users
    { duration: '5m', target: 100 }, // Stay at 100 users
    { duration: '2m', target: 0 }, // Ramp down to 0 users
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000'], // 95% of requests must complete below 2s
    http_req_failed: ['rate<0.1'], // Error rate must be below 10%
    errors: ['rate<0.1'], // Custom error rate must be below 10%
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://trust-portal-staging.atlasbank.com';
const API_KEY = __ENV.REGULATOR_API_KEY || 'dev-reg-key';

export default function () {
  const testCases = [
    // Health check
    {
      name: 'Health Check',
      url: `${BASE_URL}/health`,
      method: 'GET',
      expectedStatus: 200,
    },
    
    // Portal page
    {
      name: 'Portal Page',
      url: `${BASE_URL}/portal`,
      method: 'GET',
      expectedStatus: 200,
    },
    
    // Badge generation
    {
      name: 'Badge Generation V1',
      url: `${BASE_URL}/api/v1/badge/test-entity-${Math.random()}.svg`,
      method: 'GET',
      expectedStatus: 200,
    },
    
    // Badge generation V2
    {
      name: 'Badge Generation V2',
      url: `${BASE_URL}/api/v2/badge/test-entity-${Math.random()}.svg`,
      method: 'GET',
      expectedStatus: 200,
    },
    
    // Open data index
    {
      name: 'Open Data Index V1',
      url: `${BASE_URL}/api/v1/opendata/index.json`,
      method: 'GET',
      expectedStatus: 200,
    },
    
    // Open data index V2
    {
      name: 'Open Data Index V2',
      url: `${BASE_URL}/api/v2/opendata/index.json`,
      method: 'GET',
      expectedStatus: 200,
    },
    
    // Regulator API V1
    {
      name: 'Regulator API V1',
      url: `${BASE_URL}/api/v1/regulator/entities/test-entity-${Math.random()}/trust`,
      method: 'GET',
      headers: { 'X-API-Key': API_KEY },
      expectedStatus: 200,
    },
    
    // Regulator API V2
    {
      name: 'Regulator API V2',
      url: `${BASE_URL}/api/v2/regulator/entities/test-entity-${Math.random()}/trust`,
      method: 'GET',
      headers: { 'X-API-Key': API_KEY },
      expectedStatus: 200,
    },
  ];

  // Randomly select a test case
  const testCase = testCases[Math.floor(Math.random() * testCases.length)];
  
  const params = {
    headers: testCase.headers || {},
    timeout: '30s',
  };

  let response;
  if (testCase.method === 'GET') {
    response = http.get(testCase.url, params);
  } else if (testCase.method === 'POST') {
    response = http.post(testCase.url, testCase.body || '', params);
  }

  const success = check(response, {
    [`${testCase.name} - Status is ${testCase.expectedStatus}`]: (r) => r.status === testCase.expectedStatus,
    [`${testCase.name} - Response time < 2s`]: (r) => r.timings.duration < 2000,
    [`${testCase.name} - No server errors`]: (r) => r.status < 500,
  });

  errorRate.add(!success);

  // Log errors for debugging
  if (!success) {
    console.error(`Test failed: ${testCase.name}`);
    console.error(`Status: ${response.status}`);
    console.error(`Response: ${response.body}`);
  }

  sleep(1);
}

export function handleSummary(data) {
  return {
    'performance-results.json': JSON.stringify(data, null, 2),
    'performance-summary.html': htmlReport(data),
  };
}

function htmlReport(data) {
  return `
    <!DOCTYPE html>
    <html>
    <head>
        <title>Trust Portal Performance Test Results</title>
        <style>
            body { font-family: Arial, sans-serif; margin: 20px; }
            .metric { margin: 10px 0; padding: 10px; border: 1px solid #ddd; }
            .pass { background-color: #d4edda; }
            .fail { background-color: #f8d7da; }
        </style>
    </head>
    <body>
        <h1>Trust Portal Performance Test Results</h1>
        <div class="metric ${data.metrics.http_req_duration.values.p95 < 2000 ? 'pass' : 'fail'}">
            <strong>95th Percentile Response Time:</strong> ${data.metrics.http_req_duration.values.p95}ms
            <br><small>Threshold: < 2000ms</small>
        </div>
        <div class="metric ${data.metrics.http_req_failed.values.rate < 0.1 ? 'pass' : 'fail'}">
            <strong>Error Rate:</strong> ${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)}%
            <br><small>Threshold: < 10%</small>
        </div>
        <div class="metric ${data.metrics.errors.values.rate < 0.1 ? 'pass' : 'fail'}">
            <strong>Custom Error Rate:</strong> ${(data.metrics.errors.values.rate * 100).toFixed(2)}%
            <br><small>Threshold: < 10%</small>
        </div>
        <h2>Test Summary</h2>
        <p>Total Requests: ${data.metrics.http_reqs.values.count}</p>
        <p>Average Response Time: ${data.metrics.http_req_duration.values.avg}ms</p>
        <p>Max Response Time: ${data.metrics.http_req_duration.values.max}ms</p>
        <p>Test Duration: ${data.state.testRunDurationMs}ms</p>
    </body>
    </html>
  `;
}

