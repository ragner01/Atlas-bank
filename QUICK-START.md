# 🚀 AtlasBank Quick Start Guide

## **Immediate Testing (5 Minutes)**

### **Prerequisites**
- Docker Desktop running
- .NET 8 SDK installed
- Git installed

### **Step 1: Clone and Build**
```bash
# Clone the repository
git clone https://github.com/ragner01/Atlas-bank.git
cd Atlas-bank

# Build the solution
dotnet build
```

### **Step 2: Start Services**
```bash
# Start all services with Docker Compose
make up

# Wait for services to start (about 30 seconds)
# Check service health
curl http://localhost:5181/health  # Ledger Service
curl http://localhost:5191/health  # Payments Service
curl http://localhost:5080/health  # API Gateway
```

### **Step 3: Test Core Functionality**
```bash
# Test payment transfer
curl -X POST http://localhost:5191/payments/transfers \
  -H 'Idempotency-Key: test-123' \
  -H 'Content-Type: application/json' \
  -d '{
    "SourceAccountId": "acc_A",
    "DestinationAccountId": "acc_B", 
    "Minor": 100000,
    "Currency": "NGN",
    "Narration": "Test transfer"
  }'

# Test AML monitoring
make aml-test

# Check AML worker logs
docker logs amlworker -f
```

### **Step 4: Test gRPC Communication**
```bash
# Test Ledger gRPC service
grpcurl -plaintext localhost:7001 list
grpcurl -plaintext localhost:7001 ledger.v1.LedgerService.PostEntry
```

---

## **Production Deployment (30 Minutes)**

### **Step 1: Azure Setup**
```bash
# Login to Azure
az login

# Create resource group
az group create --name atlasbank-rg --location eastus

# Create service principal
az ad sp create-for-rbac --name "atlasbank-github" \
  --role contributor \
  --scopes /subscriptions/{subscription-id}/resourceGroups/atlasbank-rg
```

### **Step 2: Configure GitHub Secrets**
Go to: `https://github.com/ragner01/Atlas-bank/settings/secrets/actions`

Add these secrets:
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_CLIENT_ID`
- `AZURE_CLIENT_SECRET`
- `AZURE_RG=atlasbank-rg`
- `AZURE_AKS=atlasbank-aks`

### **Step 3: Deploy Infrastructure**
```bash
# Navigate to Terraform directory
cd infrastructure/iac/terraform/azure

# Initialize and deploy
terraform init
terraform plan -var="prefix=atlasbank" -var="location=eastus"
terraform apply -var="prefix=atlasbank" -var="location=eastus"
```

### **Step 4: Deploy to Kubernetes**
```bash
# Get AKS credentials
az aks get-credentials --resource-group atlasbank-rg --name atlasbank-aks

# Deploy services
kubectl apply -f infrastructure/k8s/

# Verify deployment
kubectl get pods -A
kubectl get services
```

---

## **API Testing Examples**

### **Ledger Service**
```bash
# Post journal entry
curl -X POST http://localhost:5181/ledger/entries \
  -H 'Content-Type: application/json' \
  -d '{
    "SourceAccountId": "acc_001",
    "DestinationAccountId": "acc_002",
    "Minor": 50000,
    "Currency": "NGN",
    "Narration": "Salary payment"
  }'

# Get account balance
curl http://localhost:5181/ledger/accounts/acc_001/balance
```

### **Payments Service**
```bash
# Internal transfer
curl -X POST http://localhost:5191/payments/transfers \
  -H 'Idempotency-Key: transfer-001' \
  -H 'Content-Type: application/json' \
  -d '{
    "SourceAccountId": "acc_001",
    "DestinationAccountId": "acc_002",
    "Minor": 25000,
    "Currency": "NGN",
    "Narration": "Bill payment"
  }'
```

### **API Gateway**
```bash
# Route through gateway
curl -X POST http://localhost:5080/payments/transfers \
  -H 'Idempotency-Key: gateway-test' \
  -H 'X-Tenant-Id: tnt_demo' \
  -H 'Content-Type: application/json' \
  -d '{
    "SourceAccountId": "acc_001",
    "DestinationAccountId": "acc_002",
    "Minor": 10000,
    "Currency": "NGN",
    "Narration": "Gateway test"
  }'
```

---

## **Monitoring & Observability**

### **Service Health**
```bash
# Check all service health endpoints
curl http://localhost:5181/health  # Ledger
curl http://localhost:5191/health  # Payments
curl http://localhost:5080/health  # Gateway

# Check Jaeger tracing (if running)
open http://localhost:16686

# Check Grafana dashboards (if running)
open http://localhost:3000
```

### **Logs**
```bash
# View service logs
docker logs ledgerapi -f
docker logs paymentsapi -f
docker logs amlworker -f

# View all logs
docker-compose -f infrastructure/docker/docker-compose.yml logs -f
```

---

## **Performance Testing**

### **Load Testing with k6**
```bash
# Install k6
brew install k6  # macOS
# or
curl https://github.com/grafana/k6/releases/download/v0.47.0/k6-v0.47.0-linux-amd64.tar.gz | tar xvz

# Run load test
cd tests/performance
k6 run atlasbank-load-test.js

# Run stress test
k6 run stress-test.js
```

### **Load Test Script Example**
```javascript
import http from 'k6/http';
import { check } from 'k6';

export let options = {
  vus: 10, // 10 virtual users
  duration: '30s', // for 30 seconds
};

export default function() {
  let response = http.post('http://localhost:5191/payments/transfers', 
    JSON.stringify({
      SourceAccountId: 'acc_001',
      DestinationAccountId: 'acc_002',
      Minor: 1000,
      Currency: 'NGN',
      Narration: 'Load test'
    }),
    {
      headers: {
        'Content-Type': 'application/json',
        'Idempotency-Key': `test-${__VU}-${__ITER}`
      }
    }
  );
  
  check(response, {
    'status is 202': (r) => r.status === 202,
    'response time < 200ms': (r) => r.timings.duration < 200,
  });
}
```

---

## **Troubleshooting**

### **Common Issues**

**1. Docker not running**
```bash
# Start Docker Desktop
# Check Docker status
docker ps
```

**2. Port conflicts**
```bash
# Check what's using ports
lsof -i :5181
lsof -i :5191
lsof -i :5080

# Kill processes if needed
kill -9 <PID>
```

**3. Build errors**
```bash
# Clean and rebuild
dotnet clean
dotnet build --verbosity normal
```

**4. Service not responding**
```bash
# Check service logs
docker logs ledgerapi
docker logs paymentsapi

# Restart services
make down
make up
```

### **Health Check Commands**
```bash
# Check all services
make health

# Check specific service
curl -f http://localhost:5181/health || echo "Ledger service down"
curl -f http://localhost:5191/health || echo "Payments service down"
curl -f http://localhost:5080/health || echo "Gateway service down"
```

---

## **Next Steps**

1. **Explore the Code**: Review the source code in `src/` directory
2. **Read Documentation**: Check `README.md` and `DEPLOYMENT-GUIDE.md`
3. **Test APIs**: Use the provided Postman collection
4. **Deploy to Production**: Follow the deployment checklist
5. **Monitor Performance**: Set up observability dashboards

**AtlasBank is ready for testing and production deployment! 🚀**
