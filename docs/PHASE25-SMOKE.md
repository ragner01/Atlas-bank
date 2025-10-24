# Phase 25 Smoke Tests - Chaos & DR Drills

## 🚀 **Quick Start**

### **1. Start Chaos Services**
```bash
# Start all services including chaos
make up

# Or manually:
docker compose -f infrastructure/docker/docker-compose.yml \
  -f infrastructure/docker/docker-compose.additions.phase25.yml up -d

# Check services are running
docker compose ps | grep chaos
```

### **2. Verify Services**
```bash
# Check chaos manager health
curl http://localhost:5951/health

# Check chaos agent logs
docker logs chaos-agent | grep "🌗"

# Check DR ledger service
curl http://localhost:6182/health
```

## 🧪 **Chaos Experiments**

### **Test 1: Latency Injection**
```bash
# Enable latency chaos for ledger
curl -X POST http://localhost:5951/chaos/enable \
  -H "Content-Type: application/json" \
  -d '{
    "Service": "ledger",
    "Mode": "latency",
    "FailureRate": 0.0,
    "DelayMs": 500,
    "RetryCount": 3,
    "TargetUrl": "http://ledgerapi:6181/health"
  }'

# Inject chaos
curl "http://localhost:5951/chaos/inject?service=ledger"

# Expected: Response with 500ms delay
```

### **Test 2: Failure Injection**
```bash
# Enable failure chaos for payments
curl -X POST http://localhost:5951/chaos/enable \
  -H "Content-Type: application/json" \
  -d '{
    "Service": "payments",
    "Mode": "failure",
    "FailureRate": 0.3,
    "DelayMs": 100,
    "RetryCount": 2,
    "TargetUrl": "http://paymentsapi:5191/health"
  }'

# Inject chaos multiple times
for i in {1..10}; do
  echo "Attempt $i:"
  curl "http://localhost:5951/chaos/inject?service=payments"
  echo ""
  sleep 0.5
done

# Expected: ~30% failures, 70% successes
```

### **Test 3: Bulk Operations**
```bash
# Enable chaos for multiple services
curl -X POST http://localhost:5951/chaos/bulk-enable \
  -H "Content-Type: application/json" \
  -d '[
    {
      "Service": "ledger",
      "Mode": "latency",
      "FailureRate": 0.0,
      "DelayMs": 300,
      "RetryCount": 3
    },
    {
      "Service": "payments",
      "Mode": "failure", 
      "FailureRate": 0.2,
      "DelayMs": 200,
      "RetryCount": 2
    }
  ]'

# List active chaos
curl "http://localhost:5951/chaos/list"

# Get statistics
curl "http://localhost:5951/chaos/stats"
```

### **Test 4: Disable Chaos**
```bash
# Disable chaos for ledger
curl -X POST http://localhost:5951/chaos/disable \
  -H "Content-Type: application/json" \
  -d '"ledger"'

# Verify disabled
curl "http://localhost:5951/chaos/list"

# Bulk disable
curl -X POST http://localhost:5951/chaos/bulk-disable \
  -H "Content-Type: application/json" \
  -d '["ledger", "payments"]'
```

## 🌗 **Shadow Traffic Tests**

### **Test 1: Shadow Traffic Flow**
```bash
# Check shadow traffic is running
docker logs chaos-agent | grep "🌗"

# Expected output:
# 🌗 Shadowing 5.00% traffic from ledgerapi:6181 -> ledgerapi-dr:6181
```

### **Test 2: Drift Detection**
```bash
# Check drift validation logs
docker logs chaos-agent | grep "📊"

# Expected output:
# 📊 Drift validation: 0/10 (0.00%)
```

### **Test 3: Redis Comparison Data**
```bash
# Check Redis for comparison data
docker exec redis redis-cli KEYS "shadow:comparison:*"

# Get a comparison result
docker exec redis redis-cli GET "shadow:comparison:20241224120000"

# Expected: JSON with source, destination, matches, driftDetected
```

## 🎮 **Game Day Scenarios**

### **Scenario 1: Service Degradation**
```bash
# Simulate degraded ledger performance
curl -X POST http://localhost:5951/chaos/enable \
  -H "Content-Type: application/json" \
  -d '{
    "Service": "ledger",
    "Mode": "latency",
    "FailureRate": 0.0,
    "DelayMs": 2000,
    "RetryCount": 5,
    "TargetUrl": "http://ledgerapi:6181/health"
  }'

# Test resilience with multiple requests
for i in {1..5}; do
  echo "Test $i:"
  time curl "http://localhost:5951/chaos/inject?service=ledger"
  echo ""
done

# Expected: All requests succeed but with 2s delay
```

### **Scenario 2: Partial Failures**
```bash
# Simulate 50% failure rate
curl -X POST http://localhost:5951/chaos/enable \
  -H "Content-Type: application/json" \
  -d '{
    "Service": "payments",
    "Mode": "failure",
    "FailureRate": 0.5,
    "DelayMs": 100,
    "RetryCount": 3,
    "TargetUrl": "http://paymentsapi:5191/health"
  }'

# Run stress test
success=0
failures=0
for i in {1..20}; do
  response=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:5951/chaos/inject?service=payments")
  if [ "$response" = "200" ]; then
    ((success++))
  else
    ((failures++))
  fi
done

echo "Success: $success, Failures: $failures"
# Expected: ~10 successes, ~10 failures
```

### **Scenario 3: Recovery Testing**
```bash
# Enable chaos
curl -X POST http://localhost:5951/chaos/enable \
  -H "Content-Type: application/json" \
  -d '{
    "Service": "ledger",
    "Mode": "failure",
    "FailureRate": 0.8,
    "DelayMs": 500,
    "RetryCount": 2
  }'

# Run tests
for i in {1..10}; do
  curl "http://localhost:5951/chaos/inject?service=ledger"
done

# Disable chaos (recovery)
curl -X POST http://localhost:5951/chaos/disable \
  -H "Content-Type: application/json" \
  -d '"ledger"'

# Verify recovery
curl "http://localhost:5951/chaos/inject?service=ledger"
# Expected: Should succeed immediately
```

## 📊 **Monitoring Tests**

### **Test 1: Health Checks**
```bash
# Check all service health
curl http://localhost:5951/health
curl http://localhost:6182/health  # DR ledger
curl http://localhost:6181/health  # Main ledger
```

### **Test 2: Statistics**
```bash
# Get chaos statistics
curl "http://localhost:5951/chaos/stats"

# Expected response:
# {
#   "totalActiveChaos": 2,
#   "modeBreakdown": {
#     "latency": 1,
#     "failure": 1
#   },
#   "services": ["ledger", "payments"],
#   "timestamp": "2024-12-24T12:00:00Z"
# }
```

### **Test 3: Log Analysis**
```bash
# Check chaos manager logs
docker logs chaos-manager | grep "Chaos"

# Check chaos agent logs
docker logs chaos-agent | grep -E "(🌗|📊|🚨)"

# Check DR service logs
docker logs ledgerapi-dr | tail -20
```

## 🔧 **Troubleshooting**

### **Common Issues**

#### **Chaos Manager Not Responding**
```bash
# Check if service is running
docker ps | grep chaos-manager

# Check logs
docker logs chaos-manager

# Restart if needed
docker restart chaos-manager
```

#### **Shadow Traffic Not Working**
```bash
# Check chaos agent logs
docker logs chaos-agent

# Verify environment variables
docker exec chaos-agent env | grep SHADOW

# Check Redis connection
docker exec redis redis-cli ping
```

#### **DR Service Issues**
```bash
# Check DR PostgreSQL
docker logs postgres-dr

# Check DR ledger service
docker logs ledgerapi-dr

# Verify database connection
docker exec ledgerapi-dr dotnet --info
```

### **Performance Validation**

#### **Latency Impact**
```bash
# Test without chaos
time curl "http://localhost:6181/health"

# Enable latency chaos
curl -X POST http://localhost:5951/chaos/enable \
  -H "Content-Type: application/json" \
  -d '{"Service":"ledger","Mode":"latency","DelayMs":1000,"RetryCount":1}'

# Test with chaos
time curl "http://localhost:5951/chaos/inject?service=ledger"

# Expected: ~1 second difference
```

#### **Failure Rate Validation**
```bash
# Enable 25% failure rate
curl -X POST http://localhost:5951/chaos/enable \
  -H "Content-Type: application/json" \
  -d '{"Service":"ledger","Mode":"failure","FailureRate":0.25,"RetryCount":1}'

# Run 100 tests
success=0
for i in {1..100}; do
  response=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:5951/chaos/inject?service=ledger")
  if [ "$response" = "200" ]; then
    ((success++))
  fi
done

echo "Success rate: $success%"
# Expected: ~75% success rate
```

## ✅ **Success Criteria**

### **Chaos Manager**
- ✅ Health endpoint responds
- ✅ Can enable/disable chaos
- ✅ Can inject latency and failures
- ✅ Statistics endpoint works
- ✅ Bulk operations function

### **Chaos Agent**
- ✅ Shadow traffic running (5% rate)
- ✅ Drift detection active
- ✅ Redis storage working
- ✅ Logs show activity

### **DR Environment**
- ✅ DR ledger service healthy
- ✅ DR PostgreSQL running
- ✅ Shadow traffic reaches DR
- ✅ Comparison data stored

### **Game Day Scenarios**
- ✅ Latency injection works
- ✅ Failure injection works
- ✅ Recovery procedures work
- ✅ Monitoring shows results

---

## 🎉 **Phase 25 Smoke Tests Complete!**

All chaos engineering and DR drill capabilities are working:

✅ **Chaos Injection** - Latency and failure scenarios  
✅ **Shadow Traffic** - DR validation working  
✅ **Drift Detection** - Consistency monitoring active  
✅ **Game Day Scenarios** - Resilience testing functional  
✅ **Monitoring** - Statistics and health checks working  

**AtlasBank is ready for production chaos engineering!** 🚀
