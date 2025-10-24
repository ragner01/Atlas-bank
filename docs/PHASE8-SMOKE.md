# Quick Run
1) Compose with perf overrides:
   docker compose -f infrastructure/docker/docker-compose.yml -f infrastructure/docker/docker-compose.additions.phase8.yml up -d
2) Apply SQL (auto via ledger-migrator). Then warmup:
   curl -s -X POST 'http://localhost:5181/ledger/fast-transfer?sourceAccountId=acc_A&destinationAccountId=acc_B&minor=10000&currency=NGN&narration=test&tenantId=tnt_demo' -H 'Idempotency-Key: a1'
3) Load test (5k RPS):
   BASE_URL=http://localhost:5191 k6 run infrastructure/devops/k6/transfer-5k-rps.js
