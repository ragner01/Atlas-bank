# Phase 21 — Instant Payments Switch Adapter (NIP-style)

## Overview
The NIP Gateway service provides a standardized interface for processing outward transfers through the Nigerian Instant Payment (NIP) system. It implements a 3-phase processing model: VALIDATE → SEND → ADVISE, ensuring reliability and idempotency.

## Architecture

### Service Components
- **Atlas.NipGateway**: Main service handling NIP transfers
- **PostgreSQL**: For idempotency tracking and ledger operations
- **Kafka/Redpanda**: For asynchronous message processing

### Processing Flow

#### 1. Credit Transfer (`/nip/credit-transfer`)
```
VALIDATE → SEND → ADVISE
```

**VALIDATE Phase:**
- Check account balance sufficiency
- Validate idempotency key
- Lock idempotency for tenant+key combination

**SEND Phase:**
- Produce message to `nip-out` Kafka topic
- Return `PENDING_SEND` status
- Amount held in suspense until advice received

**ADVISE Phase:**
- Process incoming advice from switch
- Finalize ledger entries on success
- Release reservations on failure

#### 2. Advice Processing (`/nip/advice`)
- Receives confirmation from external switch/bank
- Calls stored procedure to execute final ledger entries
- Handles both success and failure scenarios

## API Endpoints

### POST `/nip/credit-transfer`
Initiates an outward NIP transfer.

**Headers:**
- `X-Tenant-Id`: Tenant identifier (default: "tnt_demo")
- `Idempotency-Key`: Unique request identifier

**Request Body:**
```json
{
  "sourceAccountId": "acc_123",
  "destinationAccountId": "acc_456", 
  "minor": 125000,
  "currency": "NGN",
  "narration": "Transfer to John Doe",
  "beneficiaryBank": "044",
  "beneficiaryName": "John Doe",
  "reference": "REF123456"
}
```

**Response:**
```json
{
  "key": "abc123def456",
  "status": "PENDING_SEND",
  "tenant": "tnt_demo"
}
```

### POST `/nip/advice`
Processes incoming advice from the switch.

**Request Body:**
```json
{
  "tenantId": "tnt_demo",
  "key": "abc123def456",
  "sourceAccountId": "acc_123",
  "destinationAccountId": "acc_456",
  "minor": 125000,
  "currency": "NGN", 
  "reference": "REF123456",
  "status": "SUCCESS"
}
```

### GET `/nip/status/{key}`
Checks the status of a NIP transfer.

**Response:**
```json
{
  "key": "abc123def456",
  "status": "PENDING_SEND"
}
```

## Database Integration

### Idempotency Tracking
- Uses `request_keys` table to prevent duplicate processing
- Key format: `{tenant}::{idempotency_key}`
- PostgreSQL unique constraint ensures atomicity

### Ledger Operations
- Calls `sp_idem_transfer_execute` stored procedure for final settlement
- Integrates with existing ledger infrastructure
- Supports multi-tenant operations

## Kafka Integration

### Topics
- **`nip-out`**: Outbound transfer messages to switch
- Message format includes all transfer details plus metadata

### Producer Configuration
- `Acks.All`: Ensures message durability
- `EnableIdempotence=true`: Prevents duplicate messages
- Bootstrap servers configurable via environment

## Security Features

### Container Security
- Non-root user execution (UID 1010)
- Minimal Alpine Linux base image
- Health checks using `wget` (no curl dependency)

### Request Security
- Idempotency key validation
- Tenant isolation
- Structured logging with request correlation

## Configuration

### Environment Variables
- `LEDGER_CONN`: PostgreSQL connection string
- `KAFKA_BOOTSTRAP`: Kafka bootstrap servers
- `ASPNETCORE_URLS`: Service binding URL

### Logging
- Structured logging with Serilog
- Request/response correlation IDs
- File rotation with 30-day retention

## Error Handling

### Insufficient Funds
- Returns HTTP 402 with descriptive message
- Logs warning with account details

### Duplicate Requests
- Returns success with `duplicate: true` flag
- Prevents double processing

### Internal Errors
- Returns HTTP 500 with generic message
- Logs full exception details
- Maintains system stability

## Integration Points

### External Switch Integration
- Replace Kafka producer with actual switch API calls
- Implement retry logic and circuit breakers
- Add switch-specific error handling

### Settlement Integration
- Connect to settlement systems for final reconciliation
- Implement batch processing for efficiency
- Add settlement status tracking

## Monitoring

### Health Checks
- HTTP endpoint: `/health`
- Database connectivity validation
- Kafka producer health

### Metrics
- Transfer volume and success rates
- Processing latency measurements
- Error rate tracking

## Future Enhancements

### Real-time Processing
- WebSocket support for real-time status updates
- Push notifications for transfer completion

### Advanced Features
- Multi-currency support
- Batch transfer processing
- Fraud detection integration

### Compliance
- Audit trail enhancements
- Regulatory reporting capabilities
- Data retention policies

