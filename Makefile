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

# Start all services
up:
	@echo "Starting AtlasBank services..."
	docker-compose -f infrastructure/docker/docker-compose.yml up -d postgres redis redpanda jaeger grafana
	@echo "Waiting for infrastructure services to be ready..."
	sleep 10
	docker-compose -f infrastructure/docker/docker-compose.yml up -d ledgerapi paymentsapi gateway amlworker kycamlapi backoffice riskfeatures
	@echo "All services started. Gateway available at http://localhost:5080"

# Stop all services
down:
	@echo "Stopping AtlasBank services..."
	docker-compose -f infrastructure/docker/docker-compose.yml down

# Build all Docker images
build:
	@echo "Building Docker images..."
	docker-compose -f infrastructure/docker/docker-compose.yml build

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