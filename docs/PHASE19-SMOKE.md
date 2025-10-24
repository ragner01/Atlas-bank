# Phase 19 Smoke Tests

## Prerequisites

1. **Start Core Services**
   ```bash
   make up
   ```

2. **Wait for Services to be Ready**
   ```bash
   # Check service health
   curl http://localhost:5191/health  # Payments API
   curl http://localhost:5901/health  # Limits
   curl http://localhost:9090/-/healthy  # Prometheus
   curl http://localhost:3000/api/health  # Grafana
   ```

## Test 1: Policy Management

### 1.1 Get Default Policy
```bash
curl -X GET http://localhost:5901/limits/policy | jq .
```

**Expected:** JSON policy with velocity, MCC, time, and geo rules

### 1.2 Update Policy
```bash
curl -X POST http://localhost:5901/limits/policy \
  -H "Content-Type: application/json" \
  -d '{
    "version": "1.0",
    "velocity": [
      {"id": "per_actor_1h_50k", "scope": "per_actor", "window": "1h", "currency": "NGN", "maxMinor": 5000000}
    ],
    "mcc": [
      {"id": "deny_cash_like", "allow": false, "mcc": ["4829", "6011"]}
    ],
    "time": [
      {"id": "night_block", "allow": false, "cron": "0-6", "tz": "Africa/Lagos"}
    ],
    "geo": [
      {"id": "block_hotspot", "allow": false, "polygon": ["6.4654,3.4064","6.4660,3.4100","6.4620,3.4105","6.4615,3.4055"]}
    ]
  }'
```

**Expected:** `{"saved": true}`

### 1.3 Verify Policy Update
```bash
curl -X GET http://localhost:5901/limits/policy | jq '.velocity[0].maxMinor'
```

**Expected:** `5000000`

## Test 2: Limits Enforcement

### 2.1 Test Allowed Transaction
```bash
curl -X POST http://localhost:5901/limits/check \
  -H "Content-Type: application/json" \
  -d '{
    "TenantId": "tnt_demo",
    "ActorId": "acc_test123",
    "DeviceId": "dev_test456",
    "Ip": "192.168.1.100",
    "MerchantId": "m-123",
    "Currency": "NGN",
    "Minor": 100000,
    "Mcc": "5411",
    "Lat": 6.4650,
    "Lng": 3.4060,
    "LocalTimeIso": "2024-01-15T14:30:00+01:00"
  }'
```

**Expected:** `{"allowed": true, "action": "ALLOW", "reason": "ok"}`

### 2.2 Test MCC Block
```bash
curl -X POST http://localhost:5901/limits/check \
  -H "Content-Type: application/json" \
  -d '{
    "TenantId": "tnt_demo",
    "ActorId": "acc_test123",
    "DeviceId": "dev_test456",
    "Ip": "192.168.1.100",
    "MerchantId": "m-123",
    "Currency": "NGN",
    "Minor": 100000,
    "Mcc": "4829",
    "Lat": 6.4650,
    "Lng": 3.4060,
    "LocalTimeIso": "2024-01-15T14:30:00+01:00"
  }'
```

**Expected:** `{"allowed": false, "action": "HARD_BLOCK", "reason": "MCC 4829 blocked by deny_cash_like"}`

### 2.3 Test Velocity Limit
```bash
# Make multiple requests to trigger velocity limit
for i in {1..6}; do
  curl -X POST http://localhost:5901/limits/check \
    -H "Content-Type: application/json" \
    -d '{
      "TenantId": "tnt_demo",
      "ActorId": "acc_test123",
      "DeviceId": "dev_test456",
      "Ip": "192.168.1.100",
      "MerchantId": "m-123",
      "Currency": "NGN",
      "Minor": 1000000,
      "Mcc": "5411",
      "Lat": 6.4650,
      "Lng": 3.4060,
      "LocalTimeIso": "2024-01-15T14:30:00+01:00"
    }'
  echo ""
done
```

**Expected:** First 5 requests allowed, 6th request should trigger soft review

### 2.4 Test Geofence Block
```bash
curl -X POST http://localhost:5901/limits/check \
  -H "Content-Type: application/json" \
  -d '{
    "TenantId": "tnt_demo",
    "ActorId": "acc_test123",
    "DeviceId": "dev_test456",
    "Ip": "192.168.1.100",
    "MerchantId": "m-123",
    "Currency": "NGN",
    "Minor": 100000,
    "Mcc": "5411",
    "Lat": 6.4655,
    "Lng": 3.4070,
    "LocalTimeIso": "2024-01-15T14:30:00+01:00"
  }'
```

**Expected:** `{"allowed": false, "action": "HARD_BLOCK", "reason": "Geo blocked by block_hotspot"}`

## Test 3: Enforced Payments API

### 3.1 Test Enforced Charge Endpoint
```bash
curl -X POST "http://localhost:5191/payments/cnp/charge/enforced?amountMinor=1000&currency=NGN&cardToken=tok_demo&merchantId=m-123&mcc=5411" \
  -H "X-Tenant-Id: tnt_demo" \
  -H "X-Device-Id: dev_test456" \
  -H "X-Ip: 192.168.1.100" \
  -H "X-Lat: 6.4650" \
  -H "X-Lng: 3.4060" \
  -H "X-Local-Time: 2024-01-15T14:30:00+01:00"
```

**Expected:** Successful response or limits-related error

### 3.2 Test with Review Headers
```bash
curl -X POST "http://localhost:5191/payments/cnp/charge/enforced?amountMinor=1000000&currency=NGN&cardToken=tok_demo&merchantId=m-123&mcc=5411" \
  -H "X-Tenant-Id: tnt_demo" \
  -H "X-Device-Id: dev_test456" \
  -H "X-Ip: 192.168.1.100" \
  -H "X-Lat: 6.4650" \
  -H "X-Lng: 3.4060" \
  -H "X-Local-Time: 2024-01-15T14:30:00+01:00" \
  -v
```

**Expected:** Response with `X-Limit-Review: true` header

## Test 4: Canary Monitoring

### 4.1 Check Canary Status
```bash
# Check canary logs
docker logs atlas_canary --tail=10
```

**Expected:** Regular status updates every 15 seconds

### 4.2 Test Canary Health
```bash
# Check if canary is making requests
curl http://localhost:5191/health
```

**Expected:** Health check should show recent activity

## Test 5: Observability Stack

### 5.1 Check Prometheus Metrics
```bash
# Check if Prometheus is scraping metrics
curl http://localhost:9090/api/v1/targets | jq '.data.activeTargets[] | select(.job == "dotnet-apps")'
```

**Expected:** Targets should be UP

### 5.2 Check Alertmanager
```bash
# Check alertmanager configuration
curl http://localhost:9093/api/v1/status | jq '.data.config'
```

**Expected:** Configuration should show webhook receivers

### 5.3 Check Grafana
```bash
# Check Grafana health
curl http://localhost:3000/api/health
```

**Expected:** `{"database":"ok","version":"11.2.0"}`

## Test 6: Performance Testing

### 6.1 Load Test Limits API
```bash
# Simple load test
for i in {1..10}; do
  curl -X POST http://localhost:5901/limits/check \
    -H "Content-Type: application/json" \
    -d '{
      "TenantId": "tnt_demo",
      "ActorId": "acc_test'$i'",
      "DeviceId": "dev_test'$i'",
      "Ip": "192.168.1.'$i'",
      "MerchantId": "m-123",
      "Currency": "NGN",
      "Minor": 100000,
      "Mcc": "5411",
      "Lat": 6.4650,
      "Lng": 3.4060,
      "LocalTimeIso": "2024-01-15T14:30:00+01:00"
    }' &
done
wait
```

**Expected:** All requests should complete within reasonable time

### 6.2 Check Response Times
```bash
# Measure response time
time curl -X POST http://localhost:5901/limits/check \
  -H "Content-Type: application/json" \
  -d '{
    "TenantId": "tnt_demo",
    "ActorId": "acc_test123",
    "DeviceId": "dev_test456",
    "Ip": "192.168.1.100",
    "MerchantId": "m-123",
    "Currency": "NGN",
    "Minor": 100000,
    "Mcc": "5411",
    "Lat": 6.4650,
    "Lng": 3.4060,
    "LocalTimeIso": "2024-01-15T14:30:00+01:00"
  }'
```

**Expected:** Response time < 100ms

## Test 7: Alert Testing

### 7.1 Trigger Soft Review Alert
```bash
# Make many requests to trigger soft review
for i in {1..60}; do
  curl -X POST http://localhost:5901/limits/check \
    -H "Content-Type: application/json" \
    -d '{
      "TenantId": "tnt_demo",
      "ActorId": "acc_test123",
      "DeviceId": "dev_test456",
      "Ip": "192.168.1.100",
      "MerchantId": "m-123",
      "Currency": "NGN",
      "Minor": 1000000,
      "Mcc": "5411",
      "Lat": 6.4650,
      "Lng": 3.4060,
      "LocalTimeIso": "2024-01-15T14:30:00+01:00"
    }' &
done
wait
```

**Expected:** Should trigger soft review alerts

### 7.2 Check Prometheus Alerts
```bash
# Check active alerts
curl http://localhost:9090/api/v1/alerts | jq '.data.alerts[] | select(.state == "firing")'
```

**Expected:** Should show firing alerts if thresholds exceeded

## Test 8: Integration Testing

### 8.1 End-to-End Flow
```bash
# Complete flow: policy update -> limits check -> enforced payment
curl -X POST http://localhost:5901/limits/policy \
  -H "Content-Type: application/json" \
  -d '{
    "version": "1.0",
    "velocity": [
      {"id": "per_actor_1h_100k", "scope": "per_actor", "window": "1h", "currency": "NGN", "maxMinor": 10000000}
    ],
    "mcc": [
      {"id": "deny_cash_like", "allow": false, "mcc": ["4829", "6011"]}
    ],
    "time": [
      {"id": "night_block", "allow": false, "cron": "0-6", "tz": "Africa/Lagos"}
    ],
    "geo": [
      {"id": "block_hotspot", "allow": false, "polygon": ["6.4654,3.4064","6.4660,3.4100","6.4620,3.4105","6.4615,3.4055"]}
    ]
  }'

curl -X POST http://localhost:5901/limits/check \
  -H "Content-Type: application/json" \
  -d '{
    "TenantId": "tnt_demo",
    "ActorId": "acc_test123",
    "DeviceId": "dev_test456",
    "Ip": "192.168.1.100",
    "MerchantId": "m-123",
    "Currency": "NGN",
    "Minor": 100000,
    "Mcc": "5411",
    "Lat": 6.4650,
    "Lng": 3.4060,
    "LocalTimeIso": "2024-01-15T14:30:00+01:00"
  }'

curl -X POST "http://localhost:5191/payments/cnp/charge/enforced?amountMinor=100000&currency=NGN&cardToken=tok_demo&merchantId=m-123&mcc=5411" \
  -H "X-Tenant-Id: tnt_demo" \
  -H "X-Device-Id: dev_test456" \
  -H "X-Ip: 192.168.1.100" \
  -H "X-Lat: 6.4650" \
  -H "X-Lng: 3.4060" \
  -H "X-Local-Time: 2024-01-15T14:30:00+01:00"
```

**Expected:** All steps should complete successfully

## Cleanup

### Reset Policy
```bash
curl -X POST http://localhost:5901/limits/policy \
  -H "Content-Type: application/json" \
  -d '{
    "version": "1.0",
    "velocity": [
      {"id": "per_actor_1h_100k", "scope": "per_actor", "window": "1h", "currency": "NGN", "maxMinor": 10000000}
    ],
    "mcc": [
      {"id": "deny_cash_like", "allow": false, "mcc": ["4829", "6011"]}
    ],
    "time": [
      {"id": "night_block", "allow": false, "cron": "0-6", "tz": "Africa/Lagos"}
    ],
    "geo": [
      {"id": "block_hotspot", "allow": false, "polygon": ["6.4654,3.4064","6.4660,3.4100","6.4620,3.4105","6.4615,3.4055"]}
    ]
  }'
```

## Success Criteria

✅ **Policy Management:** Can create, read, and update policies  
✅ **Limits Enforcement:** Velocity, MCC, time, and geo rules work correctly  
✅ **API Integration:** Enforced endpoints work with limits  
✅ **Canary Monitoring:** Synthetic tests run continuously  
✅ **Observability:** Prometheus, Alertmanager, and Grafana are functional  
✅ **Performance:** Response times < 100ms  
✅ **Alerts:** SLO and security alerts trigger correctly  
✅ **Integration:** End-to-end flow works seamlessly  

## Troubleshooting

### Common Issues

1. **Services Not Starting**
   - Check Docker logs: `docker logs <service_name>`
   - Verify dependencies are running
   - Check port conflicts

2. **Redis Connection Issues**
   - Verify Redis is running: `docker ps | grep redis`
   - Check Redis logs: `docker logs atlas_redis`

3. **Prometheus Scraping Issues**
   - Check targets: `curl http://localhost:9090/api/v1/targets`
   - Verify service endpoints are accessible

4. **Alert Not Firing**
   - Check rule evaluation: `curl http://localhost:9090/api/v1/rules`
   - Verify alert thresholds are met

### Debug Commands

```bash
# Check all service logs
docker logs atlas_limits --tail=50
docker logs atlas_canary --tail=50
docker logs atlas_prometheus --tail=50
docker logs atlas_alertmanager --tail=50
docker logs atlas_grafana --tail=50

# Check Redis keys
docker exec atlas_redis redis-cli keys "vel:*"

# Check Prometheus metrics
curl http://localhost:9090/api/v1/query?query=up

# Check Grafana dashboards
curl http://localhost:3000/api/dashboards
```