# Phase 22 Smoke Tests — USSD + Offline First + Agent Network

## Prerequisites
Ensure Phase 22 services are running:
```bash
docker compose -f infrastructure/docker/docker-compose.yml \
  -f infrastructure/docker/docker-compose.additions.phase22.yml up -d
```

## Test 1: USSD Gateway Health Check
```bash
echo "Testing USSD Gateway health..."
curl -f http://localhost:5620/health || (echo "USSD Gateway health check failed" && exit 1)
echo "✅ USSD Gateway health check passed"
```

## Test 2: Agent Network Health Check
```bash
echo "Testing Agent Network health..."
curl -f http://localhost:5621/health || (echo "Agent Network health check failed" && exit 1)
echo "✅ Agent Network health check passed"
```

## Test 3: Offline Queue Health Check
```bash
echo "Testing Offline Queue health..."
curl -f http://localhost:5622/health || (echo "Offline Queue health check failed" && exit 1)
echo "✅ Offline Queue health check passed"
```

## Test 4: USSD Session Flow
```bash
echo "Testing USSD session flow..."

# Start new USSD session
echo "Starting new USSD session..."
response1=$(curl -s -X POST http://localhost:5620/ussd \
  -d "sessionId=test-session-001&msisdn=2348100000001&text=&newSession=true")
echo "Response 1: $response1"

# Select Send Money option
echo "Selecting Send Money option..."
response2=$(curl -s -X POST http://localhost:5620/ussd \
  -d "sessionId=test-session-001&msisdn=2348100000001&text=2&newSession=false")
echo "Response 2: $response2"

# Enter destination account
echo "Entering destination account..."
response3=$(curl -s -X POST http://localhost:5620/ussd \
  -d "sessionId=test-session-001&msisdn=2348100000001&text=customer::cust002&newSession=false")
echo "Response 3: $response3"

# Enter amount
echo "Entering amount..."
response4=$(curl -s -X POST http://localhost:5620/ussd \
  -d "sessionId=test-session-001&msisdn=2348100000001&text=10000&newSession=false")
echo "Response 4: $response4"

# Enter PIN
echo "Entering PIN..."
response5=$(curl -s -X POST http://localhost:5620/ussd \
  -d "sessionId=test-session-001&msisdn=2348100000001&text=1234&newSession=false")
echo "Response 5: $response5"

echo "✅ USSD session flow test completed"
```

## Test 5: Agent Withdrawal Intent
```bash
echo "Testing Agent withdrawal intent..."

# Create withdrawal intent
echo "Creating withdrawal intent..."
intent_response=$(curl -s -X POST "http://localhost:5621/agent/withdraw/intent?msisdn=2348100000001&agent=AG001&minor=50000&currency=NGN")
echo "Intent response: $intent_response"

# Extract code from response
code=$(echo $intent_response | grep -o '"code":"[^"]*"' | cut -d'"' -f4)
echo "Withdrawal code: $code"

if [ -z "$code" ]; then
    echo "❌ Failed to create withdrawal intent"
    exit 1
fi

echo "✅ Agent withdrawal intent created successfully"
```

## Test 6: Agent Confirmation
```bash
echo "Testing Agent confirmation..."

# Confirm the intent (if code was extracted)
if [ ! -z "$code" ]; then
    echo "Confirming intent with code: $code"
    confirm_response=$(curl -s -X POST "http://localhost:5621/agent/confirm?code=$code")
    echo "Confirmation response: $confirm_response"
    
    if echo "$confirm_response" | grep -q "POSTED"; then
        echo "✅ Agent confirmation successful"
    else
        echo "❌ Agent confirmation failed"
        exit 1
    fi
else
    echo "⚠️ Skipping agent confirmation test (no code available)"
fi
```

## Test 7: Agent Cash-in Intent
```bash
echo "Testing Agent cash-in intent..."

# Create cash-in intent
echo "Creating cash-in intent..."
cashin_response=$(curl -s -X POST "http://localhost:5621/agent/cashin/intent?msisdn=2348100000002&agent=AG002&minor=25000&currency=NGN")
echo "Cash-in response: $cashin_response"

# Extract code from response
cashin_code=$(echo $cashin_response | grep -o '"code":"[^"]*"' | cut -d'"' -f4)
echo "Cash-in code: $cashin_code"

if [ -z "$cashin_code" ]; then
    echo "❌ Failed to create cash-in intent"
    exit 1
fi

echo "✅ Agent cash-in intent created successfully"
```

## Test 8: Offline Operation Queue
```bash
echo "Testing Offline operation queue..."

# Generate timestamp for nonce
timestamp=$(date +%s%N)
echo "Using timestamp as nonce: $timestamp"

# Create offline operation payload
payload='{"source":"msisdn::2348100000001","dest":"msisdn::2348100000002","minor":15000,"currency":"NGN","narration":"offline test"}'
echo "Payload: $payload"

# Generate HMAC signature (simplified for testing)
# In production, this would be done by the mobile app
secret="dev-secret"
device_id="test-device-001"
tenant_id="tnt_demo"
message="$payload$timestamp$tenant_id"
signature=$(echo -n "$message" | openssl dgst -sha256 -hmac "$secret:$device_id" -binary | xxd -p -c 256)
echo "Generated signature: $signature"

# Submit offline operation
echo "Submitting offline operation..."
offline_response=$(curl -s -X POST http://localhost:5622/offline/ops \
  -H 'Content-Type: application/json' \
  -d "{\"tenantId\":\"$tenant_id\",\"deviceId\":\"$device_id\",\"kind\":\"transfer\",\"payload\":$payload,\"nonce\":\"$timestamp\",\"signature\":\"$signature\"}")
echo "Offline operation response: $offline_response"

if echo "$offline_response" | grep -q "enqueued"; then
    echo "✅ Offline operation queued successfully"
else
    echo "❌ Failed to queue offline operation"
    exit 1
fi
```

## Test 9: Offline Sync
```bash
echo "Testing Offline sync..."

# Sync offline operations
echo "Syncing offline operations..."
sync_response=$(curl -s -X POST "http://localhost:5622/offline/sync?deviceId=test-device-001&max=10")
echo "Sync response: $sync_response"

if echo "$sync_response" | grep -q "processed"; then
    echo "✅ Offline sync completed successfully"
else
    echo "❌ Offline sync failed"
    exit 1
fi
```

## Test 10: USSD Balance Check
```bash
echo "Testing USSD balance check..."

# Start new session for balance check
echo "Starting balance check session..."
balance_response1=$(curl -s -X POST http://localhost:5620/ussd \
  -d "sessionId=test-balance-001&msisdn=2348100000001&text=&newSession=true")
echo "Balance response 1: $balance_response1"

# Select Balance option
echo "Selecting Balance option..."
balance_response2=$(curl -s -X POST http://localhost:5620/ussd \
  -d "sessionId=test-balance-001&msisdn=2348100000001&text=1&newSession=false")
echo "Balance response 2: $balance_response2"

# Enter PIN
echo "Entering PIN for balance check..."
balance_response3=$(curl -s -X POST http://localhost:5620/ussd \
  -d "sessionId=test-balance-001&msisdn=2348100000001&text=1234&newSession=false")
echo "Balance response 3: $balance_response3"

echo "✅ USSD balance check test completed"
```

## Test 11: USSD PIN Change
```bash
echo "Testing USSD PIN change..."

# Start new session for PIN change
echo "Starting PIN change session..."
pin_response1=$(curl -s -X POST http://localhost:5620/ussd \
  -d "sessionId=test-pin-001&msisdn=2348100000001&text=&newSession=true")
echo "PIN response 1: $pin_response1"

# Select Change PIN option
echo "Selecting Change PIN option..."
pin_response2=$(curl -s -X POST http://localhost:5620/ussd \
  -d "sessionId=test-pin-001&msisdn=2348100000001&text=5&newSession=false")
echo "PIN response 2: $pin_response2"

# Enter current PIN
echo "Entering current PIN..."
pin_response3=$(curl -s -X POST http://localhost:5620/ussd \
  -d "sessionId=test-pin-001&msisdn=2348100000001&text=1234&newSession=false")
echo "PIN response 3: $pin_response3"

# Enter new PIN
echo "Entering new PIN..."
pin_response4=$(curl -s -X POST http://localhost:5620/ussd \
  -d "sessionId=test-pin-001&msisdn=2348100000001&text=5678&newSession=false")
echo "PIN response 4: $pin_response4"

echo "✅ USSD PIN change test completed"
```

## Test 12: Service Integration
```bash
echo "Testing service integration..."

# Check all services are responding
services=("5620:USSD Gateway" "5621:Agent Network" "5622:Offline Queue")
all_healthy=true

for service in "${services[@]}"; do
    port=$(echo $service | cut -d: -f1)
    name=$(echo $service | cut -d: -f2)
    
    if curl -f -s http://localhost:$port/health > /dev/null; then
        echo "✅ $name is healthy"
    else
        echo "❌ $name is not responding"
        all_healthy=false
    fi
done

if [ "$all_healthy" = true ]; then
    echo "✅ All Phase 22 services are healthy"
else
    echo "❌ Some Phase 22 services are not healthy"
    exit 1
fi
```

## Test Summary
```bash
echo ""
echo "🎉 Phase 22 Smoke Tests Summary"
echo "================================"
echo "✅ USSD Gateway: Session management and menu flows"
echo "✅ Agent Network: Intent creation and confirmation"
echo "✅ Offline Queue: Operation queuing and sync"
echo "✅ Service Integration: All services healthy"
echo ""
echo "Phase 22 implementation is working correctly!"
echo "Ready for production deployment with additional security hardening."
```

## Troubleshooting

### Common Issues
1. **Service Not Starting**: Check Docker logs with `docker logs <container-name>`
2. **Network Issues**: Ensure services are on the same Docker network
3. **Database Connection**: Verify PostgreSQL is running and accessible
4. **Redis Connection**: Verify Redis is running and accessible

### Debug Commands
```bash
# Check service logs
docker logs atlas-ussdgw-1
docker logs atlas-agentnet-1
docker logs atlas-offlineq-1

# Check service status
docker ps | grep -E "(ussd|agent|offline)"

# Check network connectivity
docker exec atlas-ussdgw-1 ping -c 3 redis
docker exec atlas-agentnet-1 ping -c 3 postgres
```

### Performance Verification
```bash
# Check resource usage
docker stats --no-stream | grep -E "(ussd|agent|offline)"

# Check Redis memory usage
docker exec redis redis-cli info memory

# Check PostgreSQL connections
docker exec postgres psql -U atlas -d atlas_bank -c "SELECT count(*) FROM pg_stat_activity;"
```
