# Phase 21 Smoke Tests — NIP Gateway

## Prerequisites
Ensure the following services are running:
- PostgreSQL (atlas database)
- Redpanda/Kafka
- Ledger API (for stored procedures)

## Service Startup

### 1. Start Phase 21 Services
```bash
make up-phase21
# or manually:
docker-compose -f infrastructure/docker/docker-compose.additions.phase21.yml up -d
```

### 2. Verify Service Health
```bash
# Check NIP Gateway health
curl -f http://localhost:5611/health

# Expected response:
# {"ok":true,"service":"Atlas.NipGateway","timestamp":"2025-01-XX..."}
```

## Test Scenarios

### Test 1: Basic NIP Credit Transfer

#### 1.1 Initiate Transfer
```bash
curl -X POST http://localhost:5611/nip/credit-transfer \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: tnt_test" \
  -H "Idempotency-Key: test-nip-001" \
  -d '{
    "sourceAccountId": "acc_123",
    "destinationAccountId": "acc_456",
    "minor": 50000,
    "currency": "NGN",
    "narration": "Test NIP transfer",
    "beneficiaryBank": "044",
    "beneficiaryName": "John Doe",
    "reference": "NIP001"
  }'
```

**Expected Response:**
```json
{
  "key": "test-nip-001",
  "status": "PENDING_SEND",
  "tenant": "tnt_test"
}
```

#### 1.2 Check Transfer Status
```bash
curl -f http://localhost:5611/nip/status/test-nip-001
```

**Expected Response:**
```json
{
  "key": "test-nip-001",
  "status": "PENDING_SEND"
}
```

### Test 2: Duplicate Request Handling

#### 2.1 Send Duplicate Request
```bash
curl -X POST http://localhost:5611/nip/credit-transfer \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: tnt_test" \
  -H "Idempotency-Key: test-nip-001" \
  -d '{
    "sourceAccountId": "acc_123",
    "destinationAccountId": "acc_456",
    "minor": 50000,
    "currency": "NGN",
    "narration": "Duplicate test",
    "beneficiaryBank": "044",
    "beneficiaryName": "John Doe",
    "reference": "NIP001"
  }'
```

**Expected Response:**
```json
{
  "duplicate": true,
  "key": "test-nip-001",
  "status": "DUPLICATE"
}
```

### Test 3: Insufficient Funds

#### 3.1 Attempt Transfer with Insufficient Balance
```bash
curl -X POST http://localhost:5611/nip/credit-transfer \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: tnt_test" \
  -H "Idempotency-Key: test-nip-insufficient" \
  -d '{
    "sourceAccountId": "acc_123",
    "destinationAccountId": "acc_456",
    "minor": 999999999,
    "currency": "NGN",
    "narration": "Insufficient funds test",
    "beneficiaryBank": "044",
    "beneficiaryName": "John Doe",
    "reference": "NIP002"
  }'
```

**Expected Response:**
- HTTP 402 status code
- Error message: "insufficient funds"

### Test 4: Advice Processing

#### 4.1 Send Success Advice
```bash
curl -X POST http://localhost:5611/nip/advice \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "tnt_test",
    "key": "test-nip-001",
    "sourceAccountId": "acc_123",
    "destinationAccountId": "acc_456",
    "minor": 50000,
    "currency": "NGN",
    "reference": "NIP001",
    "status": "SUCCESS"
  }'
```

**Expected Response:**
```json
{
  "ack": true,
  "key": "test-nip-001",
  "status": "SUCCESS"
}
```

#### 4.2 Send Failure Advice
```bash
curl -X POST http://localhost:5611/nip/advice \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "tnt_test",
    "key": "test-nip-failed",
    "sourceAccountId": "acc_123",
    "destinationAccountId": "acc_456",
    "minor": 25000,
    "currency": "NGN",
    "reference": "NIP003",
    "status": "FAILED"
  }'
```

**Expected Response:**
```json
{
  "ack": true,
  "key": "test-nip-failed",
  "status": "FAILED"
}
```

### Test 5: Multi-Tenant Support

#### 5.1 Different Tenant, Same Key
```bash
curl -X POST http://localhost:5611/nip/credit-transfer \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: tnt_different" \
  -H "Idempotency-Key: test-nip-001" \
  -d '{
    "sourceAccountId": "acc_789",
    "destinationAccountId": "acc_012",
    "minor": 75000,
    "currency": "NGN",
    "narration": "Different tenant test",
    "beneficiaryBank": "058",
    "beneficiaryName": "Jane Smith",
    "reference": "NIP004"
  }'
```

**Expected Response:**
```json
{
  "key": "test-nip-001",
  "status": "PENDING_SEND",
  "tenant": "tnt_different"
}
```

## Kafka Integration Tests

### Test 6: Message Production

#### 6.1 Check Kafka Topic
```bash
# If using Redpanda, check the nip-out topic
docker exec -it atlas-redpanda-1 rpk topic list
docker exec -it atlas-redpanda-1 rpk topic consume nip-out --num 1
```

**Expected:** Messages should be produced to `nip-out` topic with proper key-value structure.

## Error Handling Tests

### Test 7: Invalid JSON

#### 7.1 Send Malformed Request
```bash
curl -X POST http://localhost:5611/nip/credit-transfer \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: tnt_test" \
  -H "Idempotency-Key: test-malformed" \
  -d '{"invalid": json}'
```

**Expected Response:**
- HTTP 400 status code
- JSON parsing error

### Test 8: Missing Required Fields

#### 8.1 Send Incomplete Request
```bash
curl -X POST http://localhost:5611/nip/credit-transfer \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: tnt_test" \
  -H "Idempotency-Key: test-incomplete" \
  -d '{
    "sourceAccountId": "acc_123",
    "minor": 50000
  }'
```

**Expected Response:**
- HTTP 400 status code
- Validation error for missing fields

## Performance Tests

### Test 9: Concurrent Requests

#### 9.1 Send Multiple Concurrent Transfers
```bash
# Run multiple transfers simultaneously
for i in {1..5}; do
  curl -X POST http://localhost:5611/nip/credit-transfer \
    -H "Content-Type: application/json" \
    -H "X-Tenant-Id: tnt_test" \
    -H "Idempotency-Key: concurrent-test-$i" \
    -d "{
      \"sourceAccountId\": \"acc_123\",
      \"destinationAccountId\": \"acc_456\",
      \"minor\": $((10000 * i)),
      \"currency\": \"NGN\",
      \"narration\": \"Concurrent test $i\",
      \"beneficiaryBank\": \"044\",
      \"beneficiaryName\": \"Test User $i\",
      \"reference\": \"CONC$i\"
    }" &
done
wait
```

**Expected:** All requests should be processed successfully without conflicts.

## Cleanup

### Stop Services
```bash
make down-phase21
# or manually:
docker-compose -f infrastructure/docker/docker-compose.additions.phase21.yml down
```

## Troubleshooting

### Common Issues

1. **Service Not Starting**
   - Check Docker logs: `docker logs docker-nipgw-1`
   - Verify database connectivity
   - Check Kafka/Redpanda availability

2. **Database Connection Errors**
   - Ensure PostgreSQL is running
   - Verify connection string in environment variables
   - Check database permissions

3. **Kafka Connection Issues**
   - Verify Redpanda/Kafka is running
   - Check bootstrap servers configuration
   - Ensure topic creation permissions

### Log Analysis
```bash
# View NIP Gateway logs
docker logs docker-nipgw-1 -f

# Check for specific errors
docker logs docker-nipgw-1 | grep ERROR
```

## Success Criteria

All tests should pass with:
- ✅ Service health check returns 200 OK
- ✅ Credit transfers return PENDING_SEND status
- ✅ Duplicate requests are handled correctly
- ✅ Insufficient funds return 402 error
- ✅ Advice processing works for both success and failure
- ✅ Multi-tenant isolation works correctly
- ✅ Kafka messages are produced successfully
- ✅ Error handling works for invalid requests
- ✅ Concurrent requests are processed without conflicts

