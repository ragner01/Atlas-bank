# AtlasBank Makefile for Development and Testing

.PHONY: help up down build test clean logs

# Default target
help:
	@echo "AtlasBank Development Commands:"
	@echo "  make up          - Start all services with docker-compose"
	@echo "  make down        - Stop all services"
	@echo "  make build       - Build all Docker images"
	@echo "  make test        - Run end-to-end smoke tests"
	@echo "  make clean       - Clean up containers and volumes"
	@echo "  make logs        - Show logs for all services"
	@echo "  make ledger-test - Test Ledger API directly"
	@echo "  make payments-test - Test Payments API directly"
	@echo "  make gateway-test - Test API Gateway"
	@echo "  make aml-test    - Test AML Worker"
	@echo "  make limits-test - Test Limits & Controls + Observability"
	@echo "  make kyc-test   - Test KYC/AML Orchestration"
	@echo "  make nip-test   - Test NIP Gateway"

# Start all services
up:
	@echo "Starting AtlasBank services..."
	docker-compose -f infrastructure/docker/docker-compose.yml up -d postgres redis redpanda jaeger grafana
	@echo "Waiting for infrastructure services to be ready..."
	sleep 10
	docker-compose -f infrastructure/docker/docker-compose.yml up -d ledgerapi paymentsapi gateway amlworker kycamlapi backoffice riskfeatures loansapi
	@echo "Starting Phase 19 services (Limits & Observability)..."
	docker-compose -f infrastructure/docker/docker-compose.additions.phase19.yml up -d
	@echo "Starting Phase 20 services (KYC/AML Orchestration)..."
	docker-compose -f infrastructure/docker/docker-compose.additions.phase20.yml up -d
	@echo "Starting Phase 21 services (NIP Gateway)..."
	docker-compose -f infrastructure/docker/docker-compose.additions.phase21.yml up -d
	@echo "All services started. Gateway available at http://localhost:5080"

# Stop all services
down:
	@echo "Stopping AtlasBank services..."
	docker-compose -f infrastructure/docker/docker-compose.yml down
	docker-compose -f infrastructure/docker/docker-compose.additions.phase19.yml down
	docker-compose -f infrastructure/docker/docker-compose.additions.phase20.yml down
	docker-compose -f infrastructure/docker/docker-compose.additions.phase21.yml down

# Build all Docker images
build:
	@echo "Building Docker images..."
	docker-compose -f infrastructure/docker/docker-compose.yml build
	docker-compose -f infrastructure/docker/docker-compose.additions.phase19.yml build
	docker-compose -f infrastructure/docker/docker-compose.additions.phase20.yml build
	docker-compose -f infrastructure/docker/docker-compose.additions.phase21.yml build

# Run end-to-end smoke tests
test: up
	@echo "Running end-to-end smoke tests..."
	@echo "Waiting for services to be ready..."
	sleep 30
	@echo "Testing API Gateway health..."
	curl -f http://localhost:5080/health || (echo "Gateway health check failed" && exit 1)
	@echo "Testing Payments API through Gateway..."
	curl -f -X POST -H "Content-Type: application/json" -H "Idempotency-Key: 2b2a4d3b-6c7d-4db0-9a7e-0a9f9c2a1111" \
		-d '{"SourceAccountId":"acc_123","DestinationAccountId":"acc_456","Minor":125000,"Currency":"NGN","Narration":"Rent"}' \
		http://localhost:5191/payments/transfers || (echo "Payments API test failed" && exit 1)
	@echo "Testing Ledger API directly..."
	curl -f http://localhost:5181/ledger/accounts/acc_123/balance || (echo "Ledger API test failed" && exit 1)
	@echo "All smoke tests passed!"

# Test Ledger API directly
ledger-test:
	@echo "Testing Ledger API directly..."
	curl -f http://localhost:5181/health || (echo "Ledger health check failed" && exit 1)
	curl -f http://localhost:5181/ledger/accounts/acc_123/balance || (echo "Ledger account test failed" && exit 1)

# Test Payments API directly
payments-test:
	@echo "Testing Payments API directly..."
	curl -f http://localhost:5191/health || (echo "Payments health check failed" && exit 1)
	curl -f -X POST -H "Content-Type: application/json" -H "Idempotency-Key: test-key-2" \
		-d '{"SourceAccountId":"acc_123","DestinationAccountId":"acc_456","Minor":50000,"Currency":"NGN","Narration":"direct test"}' \
		http://localhost:5191/payments/transfers || (echo "Payments transfer test failed" && exit 1)

# Test API Gateway
gateway-test:
	@echo "Testing API Gateway..."
	curl -f http://localhost:5080/health || (echo "Gateway health check failed" && exit 1)

aml-test:
	@echo "Testing AML Worker with high-value transaction..."
	curl -s -X POST http://localhost:5191/payments/transfers \
		-H 'Idempotency-Key: 98e1b383-3d0a-4d8a-8e8d-5a43d501a9aa' \
		-H 'Content-Type: application/json' \
		-d '{"SourceAccountId":"acc_A","DestinationAccountId":"acc_B","Minor":75000000,"Currency":"NGN","Narration":"Test burst"}' || (echo "AML test failed" && exit 1)
	@echo "Check AML worker logs: docker logs amlworker -f"

limits-test:
	@echo "Testing Limits & Controls..."
	@echo "Testing policy management..."
	curl -f http://localhost:5901/limits/policy || (echo "Limits policy test failed" && exit 1)
	@echo "Testing limits enforcement..."
	curl -f -X POST http://localhost:5901/limits/check \
		-H "Content-Type: application/json" \
		-d '{"TenantId":"tnt_demo","ActorId":"acc_test","DeviceId":"dev_test","Ip":"192.168.1.100","MerchantId":"m-123","Currency":"NGN","Minor":100000,"Mcc":"5411","Lat":6.4650,"Lng":3.4060,"LocalTimeIso":"2024-01-15T14:30:00+01:00"}' || (echo "Limits check test failed" && exit 1)
	@echo "Testing enforced payments..."
	curl -f -X POST "http://localhost:5191/payments/cnp/charge/enforced?amountMinor=1000&currency=NGN&cardToken=tok_demo&merchantId=m-123&mcc=5411" \
		-H "X-Tenant-Id: tnt_demo" \
		-H "X-Device-Id: dev_test" \
		-H "X-Ip: 192.168.1.100" || (echo "Enforced payments test failed" && exit 1)
	@echo "Testing observability stack..."
	curl -f http://localhost:9090/-/healthy || (echo "Prometheus health check failed" && exit 1)
	curl -f http://localhost:9093/api/v1/status || (echo "Alertmanager health check failed" && exit 1)
	curl -f http://localhost:3000/api/health || (echo "Grafana health check failed" && exit 1)
	@echo "All limits tests passed!"

kyc-test:
	@echo "Testing KYC/AML Orchestration..."
	@echo "Testing KYC service health..."
	curl -f http://localhost:5801/health || (echo "KYC health check failed" && exit 1)
	@echo "Testing AML service health..."
	curl -f http://localhost:5802/health || (echo "AML health check failed" && exit 1)
	@echo "Testing Case Management service health..."
	curl -f http://localhost:5803/health || (echo "Case Management health check failed" && exit 1)
	@echo "Testing KYC flow..."
	APPLICATION_ID=$$(curl -s -X POST http://localhost:5801/kyc/start \
		-H "Content-Type: application/json" \
		-d '{"customerId": "cust_test_kyc"}' | jq -r '.applicationId') && \
	curl -X POST http://localhost:5801/kyc/bvn \
		-H "Content-Type: application/json" \
		-d "{\"applicationId\": \"$$APPLICATION_ID\", \"bvn\": \"12345678901\"}" && \
	curl -X POST http://localhost:5801/kyc/nin \
		-H "Content-Type: application/json" \
		-d "{\"applicationId\": \"$$APPLICATION_ID\", \"nin\": \"12345678901\"}" && \
	curl -X POST http://localhost:5801/kyc/selfie \
		-H "Content-Type: application/json" \
		-d "{\"applicationId\": \"$$APPLICATION_ID\", \"score\": 0.85}" && \
	curl -X POST http://localhost:5801/kyc/poa \
		-H "Content-Type: application/json" \
		-d "{\"applicationId\": \"$$APPLICATION_ID\", \"addressHash\": \"abc123def456ghi789jkl012mno345pqr678stu901vwx234yz\"}" && \
	curl -X POST http://localhost:5801/kyc/decision \
		-H "Content-Type: application/json" \
		-d "{\"applicationId\": \"$$APPLICATION_ID\", \"customerId\": \"cust_test_kyc\"}" || (echo "KYC flow test failed" && exit 1)
	@echo "Testing AML sanctions management..."
	curl -X POST http://localhost:5802/aml/sanctions/load \
		-H "Content-Type: application/json" \
		-d '{"sanctionsIds": ["sanctioned_customer_001", "sanctioned_customer_002"]}' || (echo "AML sanctions load test failed" && exit 1)
	@echo "Testing AML transaction scanning..."
	curl -X POST http://localhost:5802/aml/scan \
		-H "Content-Type: application/json" \
		-d '{"transactionId": "txn_test_001", "customerId": "cust_test_aml", "amountMinor": 1000000, "currency": "NGN", "timestamp": "2024-01-15T14:30:00Z"}' || (echo "AML scan test failed" && exit 1)
	@echo "Testing Case Management..."
	CASE_ID=$$(curl -s -X POST http://localhost:5803/cases \
		-H "Content-Type: application/json" \
		-d '{"customerId": "cust_test_case", "caseType": "SANCTIONS", "priority": "HIGH", "description": "Test case", "createdBy": "test_user"}' | jq -r '.case_id') && \
	curl -X PUT http://localhost:5803/cases/$$CASE_ID/status \
		-H "Content-Type: application/json" \
		-d '{"status": "INVESTIGATING", "updatedBy": "test_user"}' || (echo "Case management test failed" && exit 1)
	@echo "All KYC/AML tests passed!"

nip-test:
	@echo "Testing NIP Gateway..."
	@echo "Testing NIP Gateway health..."
	curl -f http://localhost:5611/health || (echo "NIP Gateway health check failed" && exit 1)
	@echo "Testing NIP credit transfer..."
	curl -X POST http://localhost:5611/nip/credit-transfer \
		-H "Content-Type: application/json" \
		-H "X-Tenant-Id: tnt_test" \
		-H "Idempotency-Key: test-nip-001" \
		-d '{"sourceAccountId":"customer::cust001","destinationAccountId":"customer::cust002","minor":50000,"currency":"USD","narration":"Test NIP transfer","beneficiaryBank":"044","beneficiaryName":"John Doe","reference":"NIP001"}' || (echo "NIP credit transfer test failed" && exit 1)
	@echo "Testing NIP advice processing..."
	curl -X POST http://localhost:5611/nip/advice \
		-H "Content-Type: application/json" \
		-d '{"tenantId":"tnt_test","key":"test-nip-001","sourceAccountId":"customer::cust001","destinationAccountId":"customer::cust002","minor":50000,"currency":"USD","reference":"NIP001","status":"SUCCESS"}' || (echo "NIP advice test failed" && exit 1)
	@echo "Testing NIP status check..."
	curl -f -H "X-Tenant-Id: tnt_test" http://localhost:5611/nip/status/test-nip-001 || (echo "NIP status check test failed" && exit 1)
	@echo "All NIP Gateway tests passed!"

# Show logs
logs:
	docker-compose -f infrastructure/docker/docker-compose.yml logs -f

# Clean up
clean:
	@echo "Cleaning up containers and volumes..."
	docker-compose -f infrastructure/docker/docker-compose.yml down -v
	docker system prune -f

# Development helpers
dev-setup: up
	@echo "Setting up development environment..."
	@echo "Services available at:"
	@echo "  API Gateway: http://localhost:5080"
	@echo "  Ledger API:  http://localhost:5181"
	@echo "  Payments API: http://localhost:5191"
	@echo "  Jaeger UI:   http://localhost:16686"
	@echo "  Grafana:     http://localhost:3000 (admin/admin)"
	@echo "  PostgreSQL:  localhost:5432"
	@echo "  Redis:       localhost:6379"
	@echo "  AML Cases API: http://localhost:5201"
	@echo "  Backoffice UI: http://localhost:5210"
	@echo "  Risk Features: http://localhost:5301"
	@echo "  Loans API:     http://localhost:5221"
	@echo "  Limits API:   http://localhost:5901"
	@echo "  Prometheus:    http://localhost:9090"
	@echo "  Grafana:       http://localhost:3000 (admin/admin)"
	@echo "  Alertmanager: http://localhost:9093"
	@echo "  KYC API:       http://localhost:5801"
	@echo "  AML API:       http://localhost:5802"
	@echo "  Case API:      http://localhost:5803"
	@echo "  NIP Gateway:   http://localhost:5611"