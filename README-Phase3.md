# AtlasBank — Phase 3 Wiring (Azure + Event Hubs, PCI)

## Overview

This phase implements the complete wiring of AtlasBank services with Azure Event Hubs (Kafka-compatible), PostgreSQL persistence, idempotent Payments→Ledger integration, YARP gateway, and PCI-compliant infrastructure.

## Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   API Gateway   │────│   Ledger API    │────│   PostgreSQL    │
│     (YARP)      │    │   (EF Core)     │    │   (SERIALIZABLE)│
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Payments API   │────│  Event Hubs     │────│   Outbox        │
│ (Idempotency)   │    │  (Kafka)        │    │  Dispatcher     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Key Features Implemented

### 1. Ledger: EF Core + SERIALIZABLE
- **Database**: PostgreSQL with SERIALIZABLE isolation for ACID compliance
- **Entities**: `AccountRow`, `JournalRow` with proper indexing
- **Repository**: `EfLedgerRepository` with CRUD operations
- **API**: RESTful endpoints for account operations

### 2. Payments: Persistent Idempotency + Ledger Integration
- **Idempotency**: EF Core-based `EfIdempotencyStore` with PostgreSQL
- **Integration**: HTTP client calls to Ledger API for account operations
- **Saga**: Complete transfer flow with debit/credit operations
- **API**: RESTful endpoints with `Idempotency-Key` header support

### 3. Messaging: Outbox → Event Hubs (Kafka)
- **Publisher**: `EventHubsKafkaPublisher` with Kafka-compatible protocol
- **Outbox**: `OutboxDispatcher` for reliable message delivery
- **Integration**: Event Hubs with private endpoints and encryption

### 4. YARP Gateway: Tenant Routing
- **Routing**: Path-based routing to Ledger and Payments APIs
- **Headers**: `X-Tenant-Id` header forwarding
- **Health**: Health checks and load balancing
- **Configuration**: Complete routing configuration

### 5. Docker Compose: Service Wiring
- **Services**: `ledgerapi`, `paymentsapi`, `gateway`
- **Dependencies**: Proper service dependencies and health checks
- **Networking**: Internal service communication
- **Databases**: Separate databases per service

### 6. Terraform: Azure Infrastructure
- **Event Hubs**: Namespace, event hubs, authorization rules
- **AKS**: Cluster with system and app node pools
- **PostgreSQL**: Flexible Server with private endpoints
- **Key Vault**: Managed HSM integration
- **Networking**: VNet, subnets, NSGs, private DNS zones

### 7. CI/CD: Secrets & Runtime Configuration
- **GitHub Secrets**: Complete secret management
- **Kubernetes Secrets**: Runtime secret configuration
- **GitHub Actions**: Full CI/CD pipeline with security scanning

### 8. PCI Gatekeeper: Security Policies
- **No Privileged**: Prevents privileged container execution
- **mTLS Sidecar**: Requires mTLS sidecar for service communication
- **Network Policies**: Enforces network segmentation
- **Resource Limits**: Mandates resource limits and requests

## Quick Start

### Prerequisites
- Docker and Docker Compose
- .NET 8 SDK
- Make (optional, for convenience commands)

### 1. Start Services
```bash
# Using Make (recommended)
make up

# Or using Docker Compose directly
docker-compose -f infrastructure/docker/docker-compose.yml up -d
```

### 2. Run Smoke Tests
```bash
# Using Make
make test

# Or manually
curl -f http://localhost:5000/health
curl -f -H "X-Tenant-Id: tnt_demo" http://localhost:5000/api/accounts/test-account
```

### 3. Test Payments Flow
```bash
curl -X POST -H "Content-Type: application/json" \
     -H "X-Tenant-Id: tnt_demo" \
     -H "Idempotency-Key: test-key-1" \
     -d '{"amount": 100.00, "fromAccount": "test-from", "toAccount": "test-to", "reference": "test transfer"}' \
     http://localhost:5000/api/transfers
```

## Service Endpoints

| Service | URL | Port | Description |
|---------|-----|------|-------------|
| API Gateway | http://localhost:5000 | 5000 | Main entry point |
| Ledger API | http://localhost:5001 | 5001 | Account operations |
| Payments API | http://localhost:5002 | 5002 | Transfer operations |
| Jaeger UI | http://localhost:16686 | 16686 | Distributed tracing |
| Grafana | http://localhost:3000 | 3000 | Monitoring dashboard |
| PostgreSQL | localhost:5432 | 5432 | Database |
| Redis | localhost:6379 | 6379 | Cache |
| Kafka | localhost:9092 | 9092 | Message broker |

## API Examples

### Ledger API
```bash
# Get account balance
curl -H "X-Tenant-Id: tnt_demo" http://localhost:5000/api/accounts/test-account

# Credit account
curl -X POST -H "Content-Type: application/json" \
     -H "X-Tenant-Id: tnt_demo" \
     -d '{"amount": 100.00}' \
     http://localhost:5000/api/accounts/test-account/credit
```

### Payments API
```bash
# Create transfer
curl -X POST -H "Content-Type: application/json" \
     -H "X-Tenant-Id: tnt_demo" \
     -H "Idempotency-Key: unique-key-123" \
     -d '{"amount": 100.00, "fromAccount": "from-123", "toAccount": "to-456", "reference": "Transfer"}' \
     http://localhost:5000/api/transfers
```

## Development Commands

```bash
# Start all services
make up

# Run smoke tests
make test

# Test individual services
make ledger-test
make payments-test
make gateway-test

# View logs
make logs

# Clean up
make clean
```

## Monitoring & Observability

- **Jaeger**: Distributed tracing at http://localhost:16686
- **Grafana**: Monitoring dashboards at http://localhost:3000 (admin/admin)
- **Health Checks**: All services expose `/health` endpoints
- **Metrics**: Prometheus-compatible metrics on port 9090+

## Security Features

- **PCI Compliance**: Gatekeeper policies enforce security requirements
- **Network Segmentation**: Private subnets and NSGs
- **Encryption**: TLS everywhere, database encryption at rest
- **Secrets Management**: Azure Key Vault integration
- **mTLS**: Mutual TLS for service-to-service communication

## Phase 4 Backlog

1. **Advanced Event Sourcing**: Complete event store implementation
2. **Saga Orchestration**: Complex business process coordination
3. **Advanced Monitoring**: Custom dashboards and alerting
4. **Performance Optimization**: Caching, connection pooling, query optimization
5. **Disaster Recovery**: Backup/restore procedures and failover testing

## Troubleshooting

### Common Issues

1. **Services not starting**: Check Docker logs with `make logs`
2. **Database connection errors**: Ensure PostgreSQL is running and accessible
3. **API Gateway routing**: Verify YARP configuration in `appsettings.json`
4. **Idempotency issues**: Check Payments database for duplicate keys

### Debug Commands

```bash
# Check service health
curl http://localhost:5000/health
curl http://localhost:5001/health
curl http://localhost:5002/health

# View Docker logs
docker-compose -f infrastructure/docker/docker-compose.yml logs ledgerapi
docker-compose -f infrastructure/docker/docker-compose.yml logs paymentsapi
docker-compose -f infrastructure/docker/docker-compose.yml logs gateway

# Check database connectivity
docker exec -it atlas-postgres psql -U atlas -d atlas_ledger_dev -c "SELECT 1;"
```

## Contributing

1. Follow the existing code structure and patterns
2. Add tests for new functionality
3. Update documentation for API changes
4. Ensure PCI compliance for security-related changes

## License

This project is licensed under the MIT License - see the LICENSE file for details.
