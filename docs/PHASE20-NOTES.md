# Phase 20: KYC/AML Orchestration (Nigeria-ready)

## Overview

Phase 20 implements a comprehensive KYC/AML orchestration system designed for Nigerian banking regulations, including BVN/NIN verification, sanctions screening, transaction monitoring, and case management workflows.

## Architecture

### Services

1. **Atlas.Kyc** (`src/Compliance/Atlas.Kyc/`)
   - KYC flow orchestration (BVN, NIN, selfie liveness, proof-of-address)
   - Decisioning engine (APPROVED/REVIEW/REJECT)
   - PostgreSQL for application and fact storage
   - Redis for sanctions list checks

2. **Atlas.Aml** (`src/Compliance/Atlas.Aml/`)
   - Sanctions list management (Redis SET)
   - Transaction scanning and flagging
   - Risk scoring and rule engine
   - Redis for sanctions and velocity tracking

3. **Atlas.Case** (`src/Compliance/Atlas.Case/`)
   - AML case management (CRUD operations)
   - Case workflow and status management
   - Notes and audit trail
   - PostgreSQL for case storage

### Database Schema

#### KYC Tables
- `kyc_applications`: Customer KYC applications with status tracking
- `kyc_facts`: Verification results (BVN, NIN, liveness, PoA) stored as JSONB

#### AML Tables
- `aml_cases`: AML cases with priority, status, and workflow
- `aml_case_notes`: Case notes and audit trail

### Integration Points

- **Payments Integration**: AML scanning triggered on high-value transactions
- **Risk Integration**: KYC decisions feed into risk scoring
- **Audit Integration**: All actions logged for compliance reporting

## KYC Flow

### 1. Application Start
```http
POST /kyc/start
{
  "customerId": "cust_001"
}
```

### 2. Verification Steps
- **BVN Verification**: 11-digit BVN validation
- **NIN Verification**: National Identification Number validation
- **Selfie Liveness**: Biometric liveness detection (score 0-1)
- **Proof of Address**: Document OCR and verification

### 3. Decision Engine
- All verifications must pass
- Customer must not be on sanctions list
- Final decision: APPROVED/REVIEW/REJECT

## AML Monitoring

### Sanctions Management
- Bulk load sanctions IDs to Redis SET
- Real-time sanctions checking
- Add/remove individual sanctions

### Transaction Scanning
- High-value transaction detection (>50,000 NGN)
- Velocity monitoring (daily limits)
- Geographic risk assessment
- Time-based risk (night transactions)

### Risk Scoring
- **MINIMAL**: Score < 0.2
- **LOW**: Score 0.2-0.5
- **MEDIUM**: Score 0.5-1.0
- **HIGH**: Score ≥ 1.0

## Case Management

### Case Types
- SANCTIONS: Customer on sanctions list
- HIGH_VALUE: High-value transaction
- VELOCITY: Velocity threshold exceeded
- GEO_RISK: Geographic risk zone
- TIME_RISK: Time-based risk
- MANUAL: Manual case creation

### Case Status Workflow
```
OPEN → INVESTIGATING → RESOLVED/ESCALATED → CLOSED
  ↓         ↓              ↓
CLOSED   CLOSED        CLOSED
```

### Priority Levels
- LOW: Standard cases
- MEDIUM: Elevated risk
- HIGH: High priority
- CRITICAL: Immediate attention

## Security & Compliance

### Data Protection
- PII data encrypted at rest
- Audit trail for all actions
- Role-based access control
- Data retention policies

### Regulatory Compliance
- BVN/NIN verification compliance
- AML transaction monitoring
- Sanctions list screening
- Case management workflows
- Audit reporting

## Configuration

### Environment Variables
- `KYC_DB`: PostgreSQL connection string
- `CASE_DB`: PostgreSQL connection string  
- `REDIS`: Redis connection string

### Database Setup
```sql
-- Run the schema creation script
\i src/Compliance/sql/001_kyc_aml.sql
```

## Monitoring & Observability

### Health Checks
- All services expose `/health` endpoints
- Database connectivity checks
- Redis connectivity checks

### Logging
- Structured logging with Serilog
- Request/response correlation IDs
- Audit trail for compliance

### Metrics
- KYC application success rates
- AML scan performance
- Case resolution times
- Sanctions check latency

## Testing

### Unit Tests
- KYC verification logic
- AML rule engine
- Case workflow validation

### Integration Tests
- End-to-end KYC flow
- AML transaction scanning
- Case management workflows

### Load Testing
- High-volume KYC applications
- Concurrent AML scans
- Case management performance

## Deployment

### Docker Compose
```bash
# Start Phase 20 services
docker-compose -f infrastructure/docker/docker-compose.additions.phase20.yml up -d
```

### Kubernetes
- Separate namespaces for compliance services
- Resource limits and requests
- Health check probes
- ConfigMaps for configuration

## Production Considerations

### Vendor Integration
- Replace mock BVN/NIN verification with actual providers
- Integrate with biometric liveness detection services
- Connect to real sanctions lists (OFAC, UN, etc.)

### Performance Optimization
- Redis clustering for high availability
- Database read replicas
- Caching strategies
- Async processing for heavy operations

### Security Hardening
- mTLS between services
- Secrets management (Azure Key Vault)
- Network policies
- Container security scanning

## Future Enhancements

### Advanced Features
- Machine learning risk scoring
- Behavioral analytics
- Real-time fraud detection
- Automated case resolution

### Integration Expansion
- Additional verification providers
- International sanctions lists
- Cross-border transaction monitoring
- Regulatory reporting automation

