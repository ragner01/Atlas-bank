# Phase 26 Smoke Tests

## Prerequisites
- Docker Compose with Phase 26 services
- Redis running for caching
- Kafka/Redpanda for messaging
- Mobile app with real-time updates

## Test 1: Start Phase 26 Services

```bash
# Start all Phase 26 services
docker compose -f infrastructure/docker/docker-compose.yml \
  -f infrastructure/docker/docker-compose.additions.phase26.yml up -d

# Verify services are running
docker compose ps

# Check service health
curl http://localhost:5851/health  # Realtime service
curl http://localhost:6181/health  # Ledger API
curl http://localhost:5191/health  # Payments API
```

**Expected Results:**
- All services return `{"ok": true}`
- Redis is accessible
- Kafka topics are created

## Test 2: Real-time Balance Updates

### 2.1 Perform a Transfer
```bash
# Create a transfer request
curl -X POST "http://localhost:5191/payments/transfers/with-risk" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{
    "SourceAccountId": "msisdn::2348100000001",
    "DestinationAccountId": "msisdn::2348100000002",
    "Minor": 1000,
    "Currency": "NGN"
  }'
```

### 2.2 Verify Cache Invalidation
```bash
# Check Redis pub/sub channels
redis-cli pubsub channels

# Should see: balance:invalidate
```

### 2.3 Test WebSocket Connection
```bash
# Connect to WebSocket endpoint
wscat -c ws://localhost:5851/ws

# Subscribe to balance updates
{"type":"invoke","method":"SubscribeBalance","args":["msisdn::2348100000001"]}

# Should receive subscription confirmation
```

**Expected Results:**
- Transfer completes successfully
- Cache invalidation messages published
- WebSocket connection established
- Balance updates received in real-time

## Test 3: Mobile App Real-time Updates

### 3.1 Open Mobile App
- Launch Expo app on device/simulator
- Login with test credentials: `2348100000001` / `1234`
- Navigate to Home screen

### 3.2 Verify Real-time Features
- Check for "Live" indicator on balance card
- Perform a transfer from another account
- Observe balance updates instantly
- Test offline/online scenarios

**Expected Results:**
- Balance updates appear instantly
- Connection status shows "Live"
- Offline fallback works correctly
- Reconnection happens automatically

## Test 4: Load Shedding

### 4.1 Generate High Load
```bash
# Install hey if not available
go install github.com/rakyll/hey@latest

# Generate load on Payments API
hey -z 30s -q 600 -m POST "http://localhost:5191/payments/transfers/with-risk" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"SourceAccountId":"msisdn::2348100000001","DestinationAccountId":"msisdn::2348100000002","Minor":1000,"Currency":"NGN"}'
```

### 4.2 Monitor Load Shedding
```bash
# Check load shedding statistics
curl http://localhost:5191/payments/load-shedding/stats

# Monitor response codes
hey -z 10s -q 1000 -m POST "http://localhost:5191/payments/transfers/with-risk" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"SourceAccountId":"msisdn::2348100000001","DestinationAccountId":"msisdn::2348100000002","Minor":1000,"Currency":"NGN"}' \
  | grep -E "(503|200|500)"
```

**Expected Results:**
- Some requests return `503 Service Unavailable`
- Response includes `Retry-After: 1` header
- Service remains stable under load
- No cascading failures

## Test 5: Cache Performance

### 5.1 Test Cache Hit Ratio
```bash
# Make multiple balance requests
for i in {1..10}; do
  curl -s "http://localhost:6181/ledger/accounts/msisdn::2348100000001/balance/global?currency=NGN" > /dev/null
done

# Check cache statistics
curl http://localhost:6181/ledger/cache/stats
```

### 5.2 Test Cache Invalidation
```bash
# Perform a transfer to invalidate cache
curl -X POST "http://localhost:5191/payments/transfers/with-risk" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{"SourceAccountId":"msisdn::2348100000001","DestinationAccountId":"msisdn::2348100000002","Minor":500,"Currency":"NGN"}'

# Immediately check balance (should be fresh)
curl "http://localhost:6181/ledger/accounts/msisdn::2348100000001/balance/global?currency=NGN"
```

**Expected Results:**
- High cache hit ratio (>80%)
- Cache invalidation works correctly
- Fresh data returned after invalidation
- Reduced database load

## Test 6: Kubernetes Autoscaling (Optional)

### 6.1 Deploy to Kubernetes
```bash
# Apply autoscaling configuration
kubectl apply -f infrastructure/k8s/phase26-autoscale-hpa-keda.yaml

# Check HPA status
kubectl get hpa -n atlas

# Check KEDA scaled objects
kubectl get scaledobjects -n atlas
```

### 6.2 Generate Load
```bash
# Create load test job
kubectl create job load-test --image=busybox --dry-run=client -o yaml | \
  kubectl apply -f -

# Monitor scaling
kubectl get pods -n atlas -w
```

**Expected Results:**
- HPA scales pods based on CPU/Memory
- KEDA scales workers based on Kafka lag
- Pods scale down when load decreases
- No service disruption during scaling

## Test 7: Error Handling

### 7.1 Test WebSocket Disconnection
```bash
# Connect and disconnect WebSocket
wscat -c ws://localhost:5851/ws &
WS_PID=$!
sleep 5
kill $WS_PID

# Should reconnect automatically
```

### 7.2 Test Redis Failure
```bash
# Stop Redis temporarily
docker compose stop redis

# Try balance request (should fall back to DB)
curl "http://localhost:6181/ledger/accounts/msisdn::2348100000001/balance/global?currency=NGN"

# Restart Redis
docker compose start redis
```

**Expected Results:**
- WebSocket reconnects automatically
- Service degrades gracefully without Redis
- Recovery happens automatically
- No data loss

## Test 8: Performance Benchmarks

### 8.1 Baseline Performance
```bash
# Measure response times
hey -n 1000 -c 10 "http://localhost:6181/ledger/accounts/msisdn::2348100000001/balance/global?currency=NGN"
```

### 8.2 Under Load Performance
```bash
# Measure performance under load
hey -z 60s -c 50 -q 100 "http://localhost:6181/ledger/accounts/msisdn::2348100000001/balance/global?currency=NGN"
```

**Expected Results:**
- P99 latency <250ms
- No timeouts under normal load
- Graceful degradation under high load
- Consistent performance

## Troubleshooting

### Common Issues

1. **WebSocket Connection Failed**
   - Check if Realtime service is running
   - Verify port 5851 is accessible
   - Check CORS configuration

2. **Cache Not Working**
   - Verify Redis is running
   - Check Redis connection string
   - Monitor Redis logs

3. **Load Shedding Too Aggressive**
   - Adjust `LS_RATE_PER_SEC` and `LS_BURST`
   - Monitor load shedding statistics
   - Check service capacity

4. **Autoscaling Not Working**
   - Verify HPA/KEDA is installed
   - Check resource metrics
   - Monitor scaling events

### Debug Commands
```bash
# Check service logs
docker compose logs realtime
docker compose logs paymentsapi
docker compose logs ledgerapi

# Check Redis status
redis-cli info

# Check Kafka topics
docker exec redpanda rpk topic list

# Check Kubernetes resources
kubectl describe hpa paymentsapi-hpa -n atlas
kubectl describe scaledobject settlement-keda -n atlas
```

## Success Criteria

✅ **All tests pass**
✅ **Real-time updates work**
✅ **Load shedding prevents failures**
✅ **Cache reduces DB load**
✅ **Autoscaling responds to load**
✅ **Mobile app updates instantly**
✅ **Service remains stable**
✅ **Performance targets met**

## Next Steps

After successful smoke tests:
1. Deploy to staging environment
2. Run comprehensive load tests
3. Monitor production metrics
4. Tune configuration parameters
5. Set up alerting and monitoring
6. Document operational procedures

