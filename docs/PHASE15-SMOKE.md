# Phase 15 Smoke Tests - Cards & Tokenization

## Prerequisites
1. Start infrastructure services (PostgreSQL, Redis, Redpanda)
2. Start Phase 15 services (Cards Vault, Network Simulator)
3. Start Payments API with card integration

## Test Commands

### 1. Start Services
```bash
# Start base infrastructure
docker compose -f infrastructure/docker/docker-compose.yml up -d postgres redis redpanda

# Start Phase 15 services
docker compose -f infrastructure/docker/docker-compose.additions.phase15.yml up -d

# Start Payments API (if not already running)
docker compose -f infrastructure/docker/docker-compose.yml up -d paymentsapi
```

### 2. Initialize Database Schema
```bash
# Connect to database and create cards table
docker exec atlas-postgres psql -U atlas -d atlas_bank -f /path/to/001_cards.sql
```

### 3. Test Card Tokenization
```bash
# Tokenize a test Visa card (use test PAN 4242424242424242)
curl -s -X POST http://localhost:5555/vault/tokenize \
  -H 'Content-Type: application/json' \
  -d '{
    "pan": "4242424242424242",
    "expiryMonth": "12",
    "expiryYear": "2028"
  }'

# Expected response:
# {
#   "card_token": "4242424242424242",  # Tokenized version
#   "network": "VISA",
#   "last4": "4242",
#   "bin": "424242",
#   "exp_m": "12",
#   "exp_y": "2028",
#   "status": "Active"
# }
```

### 4. Test Card Authorization (Inside PCI Zone)
```bash
# Test authorization with the tokenized card
curl -s -X POST http://localhost:5555/vault/authorize \
  -H 'Content-Type: application/json' \
  -d '{
    "cardToken": "PASTE_TOKEN_FROM_STEP_3",
    "amountMinor": 150000,
    "currency": "NGN",
    "merchantId": "m-123",
    "mcc": "5999"
  }'

# Expected response:
# {
#   "approved": true,
#   "auth_code": "A12345",
#   "rrn": "123456789012",
#   "network": "VISA",
#   "last4": "4242"
# }
```

### 5. Test CNP Charge (Outside PCI Zone)
```bash
# Process a card-not-present charge using token
curl -s -X POST "http://localhost:5191/payments/cnp/charge?amountMinor=150000&currency=NGN&cardToken=PASTE_TOKEN&merchantId=m-123&mcc=5999" \
  -H 'X-Tenant-Id: tnt_demo'

# Expected response:
# {
#   "status": "Accepted",
#   "entryId": "uuid",
#   "auth": "A12345",
#   "rrn": "123456789012",
#   "network": "VISA",
#   "last4": "4242"
# }
```

### 6. Test Declined Transaction
```bash
# Try a large amount that should be declined
curl -s -X POST "http://localhost:5191/payments/cnp/charge?amountMinor=500000000&currency=NGN&cardToken=PASTE_TOKEN&merchantId=m-123&mcc=5999" \
  -H 'X-Tenant-Id: tnt_demo'

# Expected response: 402 Problem "declined"
```

### 7. Test Expired Card
```bash
# Tokenize an expired card
curl -s -X POST http://localhost:5555/vault/tokenize \
  -H 'Content-Type: application/json' \
  -d '{
    "pan": "4242424242424242",
    "expiryMonth": "01",
    "expiryYear": "2020"
  }'

# Try to authorize with expired card
curl -s -X POST http://localhost:5555/vault/authorize \
  -H 'Content-Type: application/json' \
  -d '{
    "cardToken": "EXPIRED_TOKEN",
    "amountMinor": 150000,
    "currency": "NGN",
    "merchantId": "m-123",
    "mcc": "5999"
  }'

# Expected response:
# {
#   "approved": false,
#   "auth_code": "",
#   "rrn": "",
#   "reason": "expired"
# }
```

### 8. Test Restricted MCC
```bash
# Try gambling MCC (should be declined)
curl -s -X POST http://localhost:5555/vault/authorize \
  -H 'Content-Type: application/json' \
  -d '{
    "cardToken": "VALID_TOKEN",
    "amountMinor": 150000,
    "currency": "NGN",
    "merchantId": "m-123",
    "mcc": "7995"
  }'

# Expected response:
# {
#   "approved": false,
#   "auth_code": "",
#   "rrn": "",
#   "reason": "mcc_restricted"
# }
```

### 9. Verify Database State
```bash
# Check cards table
docker exec atlas-postgres psql -U atlas -d atlas_bank -c "
  SELECT card_token, bin, last4, network, status, created_at 
  FROM cards 
  ORDER BY created_at DESC 
  LIMIT 5;"

# Check audit trail
docker exec atlas-postgres psql -U atlas -d atlas_bank -c "
  SELECT card_token, operation, amount_minor, currency, status, created_at 
  FROM card_audit 
  ORDER BY created_at DESC 
  LIMIT 5;"
```

### 10. Test Health Endpoints
```bash
# Cards Vault health
curl -s http://localhost:5555/health

# Network Simulator health  
curl -s http://localhost:5601/health

# Expected responses: {"status": "ok"}
```

## Security Verification

### PAN Never Exposed Outside CDE
- Verify that PANs are never returned in API responses
- Check logs to ensure PANs only appear in Cards Vault service
- Confirm Payments API only receives tokens

### Encryption Verification
- Verify PANs are encrypted at rest in database
- Check that ciphertext blobs contain encrypted data
- Confirm DEKs are wrapped by HSM KEK

### Audit Trail
- Verify all operations are logged
- Check that audit trail contains operation details
- Confirm sensitive data is not logged

## Troubleshooting

### Common Issues
1. **Database Connection**: Ensure PostgreSQL is running and accessible
2. **Service Discovery**: Check that services can resolve each other by name
3. **Port Conflicts**: Verify ports 5555 and 5601 are available
4. **Token Format**: Ensure tokenized values maintain PAN format

### Debug Commands
```bash
# Check service logs
docker logs atlas-cardsvault
docker logs atlas-networksim

# Check database connectivity
docker exec atlas-postgres psql -U atlas -d atlas_bank -c "SELECT 1;"

# Test service connectivity
curl -v http://localhost:5555/health
curl -v http://localhost:5601/health
```
