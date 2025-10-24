# Phase 20: KYC/AML Orchestration Smoke Tests

## Prerequisites

1. **Infrastructure Running**:
   ```bash
   # Start core infrastructure
   docker-compose -f infrastructure/docker/docker-compose.yml up -d postgres redis
   
   # Wait for services to be ready
   sleep 10
   
   # Start Phase 20 services
   docker-compose -f infrastructure/docker/docker-compose.additions.phase20.yml up -d
   ```

2. **Database Schema**:
   ```bash
   # Apply KYC/AML schema
   docker exec -i postgres psql -U postgres -d atlas < src/Compliance/sql/001_kyc_aml.sql
   ```

## Service Health Checks

### 1. KYC Service Health
```bash
curl -f http://localhost:5801/health
```
**Expected**: `{"ok":true,"service":"Atlas.Kyc","timestamp":"..."}`

### 2. AML Service Health
```bash
curl -f http://localhost:5802/health
```
**Expected**: `{"ok":true,"service":"Atlas.Aml","timestamp":"..."}`

### 3. Case Management Service Health
```bash
curl -f http://localhost:5803/health
```
**Expected**: `{"ok":true,"service":"Atlas.Case","timestamp":"..."}`

## KYC Flow Testing

### 1. Start KYC Application
```bash
curl -X POST http://localhost:5801/kyc/start \
  -H "Content-Type: application/json" \
  -d '{"customerId": "cust_test_001"}'
```
**Expected**: Application ID and next steps

### 2. BVN Verification
```bash
# Get application ID from previous response
APPLICATION_ID="550e8400-e29b-41d4-a716-446655440000"

curl -X POST http://localhost:5801/kyc/bvn \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"$APPLICATION_ID\", \"bvn\": \"12345678901\"}"
```
**Expected**: `{"ok":true,"message":"BVN verified successfully","next":"nin"}`

### 3. NIN Verification
```bash
curl -X POST http://localhost:5801/kyc/nin \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"$APPLICATION_ID\", \"nin\": \"12345678901\"}"
```
**Expected**: `{"ok":true,"message":"NIN verified successfully","next":"selfie"}`

### 4. Selfie Liveness Check
```bash
curl -X POST http://localhost:5801/kyc/selfie \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"$APPLICATION_ID\", \"score\": 0.85}"
```
**Expected**: `{"ok":true,"score":0.85,"message":"Liveness check passed","next":"poa"}`

### 5. Proof of Address
```bash
curl -X POST http://localhost:5801/kyc/poa \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"$APPLICATION_ID\", \"addressHash\": \"abc123def456ghi789jkl012mno345pqr678stu901vwx234yz\"}"
```
**Expected**: `{"ok":true,"message":"Proof of address verified successfully","next":"decision"}`

### 6. KYC Decision
```bash
curl -X POST http://localhost:5801/kyc/decision \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"$APPLICATION_ID\", \"customerId\": \"cust_test_001\"}"
```
**Expected**: `{"decision":"APPROVED","reason":"All verifications passed",...}`

### 7. Check KYC Status
```bash
curl http://localhost:5801/kyc/status/$APPLICATION_ID
```
**Expected**: Application details with status

## AML Testing

### 1. Load Sanctions List
```bash
curl -X POST http://localhost:5802/aml/sanctions/load \
  -H "Content-Type: application/json" \
  -d '{"sanctionsIds": ["sanctioned_customer_001", "sanctioned_customer_002", "cust_test_001"]}'
```
**Expected**: `{"loaded":3,"added":3,"total":3}`

### 2. Check Sanctions Count
```bash
curl http://localhost:5802/aml/sanctions/count
```
**Expected**: `{"count":3}`

### 3. Check Individual Sanctions
```bash
curl http://localhost:5802/aml/sanctions/check/cust_test_001
```
**Expected**: `{"customer_id":"cust_test_001","is_sanctioned":true,...}`

### 4. Scan Transaction (High Risk)
```bash
curl -X POST http://localhost:5802/aml/scan \
  -H "Content-Type: application/json" \
  -d '{
    "transactionId": "txn_001",
    "customerId": "cust_test_001",
    "amountMinor": 6000000,
    "currency": "NGN",
    "timestamp": "2024-01-15T14:30:00Z",
    "lat": 6.4654,
    "lng": 3.4064
  }'
```
**Expected**: High risk level with multiple flags

### 5. Scan Transaction (Low Risk)
```bash
curl -X POST http://localhost:5802/aml/scan \
  -H "Content-Type: application/json" \
  -d '{
    "transactionId": "txn_002",
    "customerId": "cust_clean_001",
    "amountMinor": 100000,
    "currency": "NGN",
    "timestamp": "2024-01-15T14:30:00Z",
    "lat": 6.5000,
    "lng": 3.5000
  }'
```
**Expected**: Minimal risk level

## Case Management Testing

### 1. Create AML Case
```bash
curl -X POST http://localhost:5803/cases \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust_test_001",
    "caseType": "SANCTIONS",
    "priority": "HIGH",
    "description": "Customer flagged on sanctions list",
    "createdBy": "analyst_001"
  }'
```
**Expected**: Case ID and status

### 2. List Cases
```bash
curl "http://localhost:5803/cases?status=OPEN&priority=HIGH"
```
**Expected**: List of open high-priority cases

### 3. Get Specific Case
```bash
# Use case ID from previous response
CASE_ID="660e8400-e29b-41d4-a716-446655440000"

curl http://localhost:5803/cases/$CASE_ID
```
**Expected**: Case details

### 4. Update Case Status
```bash
curl -X PUT http://localhost:5803/cases/$CASE_ID/status \
  -H "Content-Type: application/json" \
  -d '{"status": "INVESTIGATING", "updatedBy": "analyst_002"}'
```
**Expected**: Updated case status

### 5. Add Case Note
```bash
curl -X POST http://localhost:5803/cases/$CASE_ID/notes \
  -H "Content-Type: application/json" \
  -d '{"note": "Initial investigation started. Contacting customer for additional documentation.", "createdBy": "analyst_002"}'
```
**Expected**: Note ID and details

### 6. Get Case Notes
```bash
curl http://localhost:5803/cases/$CASE_ID/notes
```
**Expected**: List of case notes

### 7. Get Case Statistics
```bash
curl http://localhost:5803/cases/stats
```
**Expected**: Case statistics by status

## End-to-End Integration Test

### Complete KYC + AML + Case Flow
```bash
#!/bin/bash

# 1. Start KYC for sanctioned customer
echo "=== Starting KYC for sanctioned customer ==="
KYC_RESPONSE=$(curl -s -X POST http://localhost:5801/kyc/start \
  -H "Content-Type: application/json" \
  -d '{"customerId": "cust_sanctioned_001"}')
echo "KYC Response: $KYC_RESPONSE"

# Extract application ID
APPLICATION_ID=$(echo $KYC_RESPONSE | jq -r '.applicationId')
echo "Application ID: $APPLICATION_ID"

# 2. Complete KYC verifications
echo "=== Completing KYC verifications ==="
curl -X POST http://localhost:5801/kyc/bvn \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"$APPLICATION_ID\", \"bvn\": \"12345678901\"}"

curl -X POST http://localhost:5801/kyc/nin \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"$APPLICATION_ID\", \"nin\": \"12345678901\"}"

curl -X POST http://localhost:5801/kyc/selfie \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"$APPLICATION_ID\", \"score\": 0.85}"

curl -X POST http://localhost:5801/kyc/poa \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"$APPLICATION_ID\", \"addressHash\": \"abc123def456ghi789jkl012mno345pqr678stu901vwx234yz\"}"

# 3. Make KYC decision (should be REJECT due to sanctions)
echo "=== Making KYC decision ==="
DECISION_RESPONSE=$(curl -s -X POST http://localhost:5801/kyc/decision \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"$APPLICATION_ID\", \"customerId\": \"cust_sanctioned_001\"}")
echo "Decision Response: $DECISION_RESPONSE"

# 4. Scan transaction for AML
echo "=== Scanning transaction for AML ==="
AML_RESPONSE=$(curl -s -X POST http://localhost:5802/aml/scan \
  -H "Content-Type: application/json" \
  -d '{
    "transactionId": "txn_sanctioned_001",
    "customerId": "cust_sanctioned_001",
    "amountMinor": 1000000,
    "currency": "NGN",
    "timestamp": "2024-01-15T14:30:00Z"
  }')
echo "AML Response: $AML_RESPONSE"

# 5. Create AML case
echo "=== Creating AML case ==="
CASE_RESPONSE=$(curl -s -X POST http://localhost:5803/cases \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust_sanctioned_001",
    "caseType": "SANCTIONS",
    "priority": "HIGH",
    "description": "Customer on sanctions list - KYC rejected",
    "createdBy": "system"
  }')
echo "Case Response: $CASE_RESPONSE"

echo "=== End-to-end test completed ==="
```

## Performance Testing

### Load Test KYC Applications
```bash
# Create multiple KYC applications concurrently
for i in {1..10}; do
  curl -X POST http://localhost:5801/kyc/start \
    -H "Content-Type: application/json" \
    -d "{\"customerId\": \"cust_load_$i\"}" &
done
wait
```

### Load Test AML Scans
```bash
# Perform multiple AML scans concurrently
for i in {1..20}; do
  curl -X POST http://localhost:5802/aml/scan \
    -H "Content-Type: application/json" \
    -d "{
      \"transactionId\": \"txn_load_$i\",
      \"customerId\": \"cust_load_$i\",
      \"amountMinor\": $((RANDOM % 1000000 + 100000)),
      \"currency\": \"NGN\",
      \"timestamp\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"
    }" &
done
wait
```

## Troubleshooting

### Common Issues

1. **Database Connection Errors**:
   ```bash
   # Check PostgreSQL status
   docker exec postgres pg_isready -U postgres
   
   # Check database exists
   docker exec postgres psql -U postgres -l
   ```

2. **Redis Connection Errors**:
   ```bash
   # Check Redis status
   docker exec redis redis-cli ping
   
   # Check Redis data
   docker exec redis redis-cli keys "*"
   ```

3. **Service Health Check Failures**:
   ```bash
   # Check service logs
   docker logs kyc
   docker logs aml
   docker logs caseapi
   
   # Check service status
   docker ps | grep -E "(kyc|aml|case)"
   ```

### Log Analysis
```bash
# Monitor KYC service logs
docker logs -f kyc

# Monitor AML service logs
docker logs -f aml

# Monitor Case service logs
docker logs -f caseapi
```

## Success Criteria

✅ **All health checks pass**  
✅ **KYC flow completes successfully**  
✅ **AML scanning detects risks correctly**  
✅ **Case management workflow functions**  
✅ **End-to-end integration works**  
✅ **Performance tests complete**  
✅ **Error handling works correctly**  

## Next Steps

After successful smoke testing:

1. **Production Deployment**: Deploy to staging environment
2. **Vendor Integration**: Connect to real BVN/NIN providers
3. **Performance Tuning**: Optimize for production load
4. **Security Hardening**: Implement production security measures
5. **Monitoring Setup**: Configure production monitoring and alerting

