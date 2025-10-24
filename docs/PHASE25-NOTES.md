# Phase 25 — Chaos & DR Drills

## 🎯 **Overview**

Phase 25 introduces **Chaos Engineering** and **Disaster Recovery (DR) Drills** to AtlasBank, enabling controlled failure injection and validation of system resilience under adverse conditions.

## 🏗️ **Architecture**

### **Core Components**

#### **1. Atlas.Chaos.Manager** 
- **Purpose**: Centralized API for managing chaos experiments
- **Port**: 5951
- **Features**:
  - Enable/disable chaos for specific services
  - Inject latency and failure scenarios
  - Monitor active chaos states
  - Bulk operations for multiple services

#### **2. Atlas.Chaos.Agent**
- **Purpose**: Background service for shadow traffic and drift validation
- **Features**:
  - Shadow 5% of live traffic to DR environment
  - Validate data consistency between main and DR
  - Store comparison results in Redis
  - Alert on drift detection

#### **3. Mock DR Environment**
- **ledgerapi-dr**: Mock disaster recovery ledger service
- **postgres-dr**: DR PostgreSQL instance
- **Purpose**: Validate DR procedures without affecting production

## 🔧 **Chaos Modes**

### **Latency Injection**
```json
{
  "Service": "ledger",
  "Mode": "latency",
  "FailureRate": 0.0,
  "DelayMs": 250,
  "RetryCount": 3,
  "TargetUrl": "http://ledgerapi:6181/health"
}
```

### **Failure Injection**
```json
{
  "Service": "payments",
  "Mode": "failure", 
  "FailureRate": 0.1,
  "DelayMs": 100,
  "RetryCount": 2,
  "TargetUrl": "http://paymentsapi:5191/health"
}
```

## 📊 **API Endpoints**

### **Chaos Management**
- `POST /chaos/enable` - Enable chaos for a service
- `POST /chaos/disable` - Disable chaos for a service
- `GET /chaos/list` - List all active chaos states
- `GET /chaos/inject?service={name}` - Inject chaos into service
- `GET /chaos/stats` - Get chaos statistics
- `POST /chaos/bulk-enable` - Enable chaos for multiple services
- `POST /chaos/bulk-disable` - Disable chaos for multiple services

### **Health & Monitoring**
- `GET /health` - Service health check
- `GET /chaos/stats` - Chaos statistics and metrics

## 🚀 **Deployment**

### **Docker Compose**
```bash
# Start chaos services
docker compose -f infrastructure/docker/docker-compose.yml \
  -f infrastructure/docker/docker-compose.additions.phase25.yml up -d

# Check status
docker compose ps | grep chaos
```

### **Environment Variables**
```bash
# Chaos Manager
ASPNETCORE_URLS=http://+:5951
ConnectionStrings__Redis=redis:6379

# Chaos Agent  
SHADOW_SRC=ledgerapi:6181
SHADOW_DST=ledgerapi-dr:6181
SHADOW_RATE=0.05
REDIS_KEY_PREFIX=shadow:
```

## 🧪 **Chaos Experiments**

### **1. Service Latency Test**
```bash
# Enable latency chaos
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

# Check stats
curl "http://localhost:5951/chaos/stats"
```

### **2. Service Failure Test**
```bash
# Enable failure chaos
curl -X POST http://localhost:5951/chaos/enable \
  -H "Content-Type: application/json" \
  -d '{
    "Service": "payments",
    "Mode": "failure",
    "FailureRate": 0.2,
    "DelayMs": 100,
    "RetryCount": 2,
    "TargetUrl": "http://paymentsapi:5191/health"
  }'

# Inject chaos multiple times
for i in {1..10}; do
  curl "http://localhost:5951/chaos/inject?service=payments"
  sleep 1
done
```

### **3. Bulk Chaos Test**
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
      "FailureRate": 0.15,
      "DelayMs": 200,
      "RetryCount": 2
    }
  ]'

# List active chaos
curl "http://localhost:5951/chaos/list"
```

## 🔍 **Shadow Traffic & Drift Detection**

### **Shadow Traffic Flow**
1. **Source**: Live service (e.g., `ledgerapi:6181`)
2. **Destination**: DR service (e.g., `ledgerapi-dr:6181`) 
3. **Rate**: 5% of traffic shadowed
4. **Validation**: Compare responses and detect drift

### **Drift Detection**
- **Automatic**: Every 5 minutes
- **Storage**: Results stored in Redis with 24h TTL
- **Alerting**: Log warnings when drift rate > 10%
- **Monitoring**: Track drift patterns over time

### **Monitoring Shadow Traffic**
```bash
# Check shadow traffic logs
docker logs chaos-agent | grep "🌗"

# Check drift validation logs  
docker logs chaos-agent | grep "📊"

# Check Redis for comparison data
docker exec redis redis-cli KEYS "shadow:comparison:*"
```

## 🎮 **Game Day Scenarios**

### **Scenario 1: Regional Outage Simulation**
```bash
# Simulate region A outage
curl -X POST http://localhost:5951/chaos/bulk-enable \
  -H "Content-Type: application/json" \
  -d '[
    {"Service": "ledger-region-a", "Mode": "failure", "FailureRate": 1.0, "DelayMs": 0, "RetryCount": 0},
    {"Service": "payments-region-a", "Mode": "failure", "FailureRate": 1.0, "DelayMs": 0, "RetryCount": 0}
  ]'

# Verify failover to region B
curl "http://localhost:5951/chaos/inject?service=ledger-region-a"
```

### **Scenario 2: Network Latency Spike**
```bash
# Simulate network issues
curl -X POST http://localhost:5951/chaos/enable \
  -H "Content-Type: application/json" \
  -d '{
    "Service": "ledger",
    "Mode": "latency",
    "FailureRate": 0.0, 
    "DelayMs": 2000,
    "RetryCount": 5
  }'

# Test resilience
for i in {1..20}; do
  curl "http://localhost:5951/chaos/inject?service=ledger"
  sleep 0.5
done
```

### **Scenario 3: Partial Service Degradation**
```bash
# Simulate degraded performance
curl -X POST http://localhost:5951/chaos/enable \
  -H "Content-Type: application/json" \
  -d '{
    "Service": "payments",
    "Mode": "failure",
    "FailureRate": 0.3,
    "DelayMs": 1000,
    "RetryCount": 3
  }'

# Monitor impact
curl "http://localhost:5951/chaos/stats"
```

## 📈 **Integration with Observability**

### **Prometheus Metrics**
- **Chaos injection rate**: `chaos_injections_total`
- **Chaos success rate**: `chaos_success_rate`
- **Drift detection rate**: `drift_detection_rate`
- **Shadow traffic volume**: `shadow_traffic_total`

### **Grafana Dashboards**
- **Chaos Engineering Overview**: Active chaos states, injection rates
- **DR Validation**: Shadow traffic metrics, drift detection
- **Service Resilience**: Success rates under chaos conditions

### **Alerting Rules**
```yaml
# High drift rate alert
- alert: HighDriftRate
  expr: drift_detection_rate > 0.1
  for: 5m
  labels:
    severity: warning
  annotations:
    summary: "High drift rate detected between main and DR"

# Chaos injection failures
- alert: ChaosInjectionFailures  
  expr: rate(chaos_injections_failed_total[5m]) > 0.1
  for: 2m
  labels:
    severity: critical
  annotations:
    summary: "Chaos injection failures detected"
```

## 🔒 **Security Considerations**

### **Access Control**
- **Authentication**: API key-based authentication for chaos endpoints
- **Authorization**: Role-based access for chaos operations
- **Audit Logging**: All chaos operations logged for compliance

### **Safety Measures**
- **Rate Limiting**: Prevent abuse of chaos injection
- **Circuit Breakers**: Automatic disable on excessive failures
- **Rollback**: Quick disable mechanism for emergency situations

## 🚀 **Production Deployment**

### **Azure Kubernetes Service**
```yaml
# chaos-manager deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: chaos-manager
spec:
  replicas: 2
  selector:
    matchLabels:
      app: chaos-manager
  template:
    metadata:
      labels:
        app: chaos-manager
    spec:
      containers:
      - name: chaos-manager
        image: atlasbank/chaos-manager:latest
        ports:
        - containerPort: 5951
        env:
        - name: ConnectionStrings__Redis
          valueFrom:
            secretKeyRef:
              name: redis-secret
              key: connection-string
```

### **GitHub Actions Integration**
```yaml
# Nightly chaos experiments
name: Nightly Chaos Experiments
on:
  schedule:
    - cron: '0 2 * * *'  # 2 AM UTC daily

jobs:
  chaos-test:
    runs-on: ubuntu-latest
    steps:
    - name: Run Chaos Experiments
      run: |
        # Enable chaos
        curl -X POST $CHAOS_MANAGER_URL/chaos/enable \
          -H "Authorization: Bearer $CHAOS_API_KEY" \
          -d '{"Service":"ledger","Mode":"latency","DelayMs":100}'
        
        # Run experiments
        for i in {1..100}; do
          curl "$CHAOS_MANAGER_URL/chaos/inject?service=ledger"
          sleep 0.1
        done
        
        # Disable chaos
        curl -X POST $CHAOS_MANAGER_URL/chaos/disable \
          -H "Authorization: Bearer $CHAOS_API_KEY" \
          -d '"ledger"'
```

## 🎯 **Benefits**

### **Resilience Validation**
- **Proactive Testing**: Find failures before customers do
- **Confidence**: Validate system behavior under stress
- **Recovery**: Test disaster recovery procedures

### **Operational Excellence**
- **Game Days**: Regular chaos experiments
- **Team Training**: Practice incident response
- **Documentation**: Improve runbooks and procedures

### **Business Continuity**
- **DR Validation**: Ensure DR environment works
- **Failover Testing**: Validate multi-region capabilities
- **Compliance**: Meet regulatory requirements for DR

## 🔮 **Future Enhancements**

### **Advanced Chaos Patterns**
- **Resource Exhaustion**: CPU, memory, disk space
- **Network Partitioning**: Split-brain scenarios
- **Clock Skew**: Time synchronization issues
- **Dependency Failures**: Cascading failures

### **Automated Recovery**
- **Self-Healing**: Automatic chaos disable on critical failures
- **Circuit Breakers**: Intelligent failure detection
- **Rollback**: Automated rollback on chaos-induced issues

### **Machine Learning**
- **Failure Prediction**: ML-based failure prediction
- **Optimal Chaos**: AI-driven chaos experiment design
- **Anomaly Detection**: ML-based drift detection

---

## 🎉 **Phase 25 Complete!**

AtlasBank now has **production-grade chaos engineering** capabilities:

✅ **Controlled Failure Injection** - Latency and failure scenarios  
✅ **Shadow Traffic Validation** - DR environment testing  
✅ **Drift Detection** - Automated consistency validation  
✅ **Game Day Scenarios** - Comprehensive resilience testing  
✅ **Observability Integration** - Metrics, alerts, and dashboards  
✅ **Production Ready** - Security, safety, and operational excellence  

**AtlasBank is now resilient to failures and ready for production chaos!** 🚀

