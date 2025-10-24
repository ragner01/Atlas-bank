# Smoke
1) Start services with Phase 8 compose + add Phase 9:
   docker compose -f infrastructure/docker/docker-compose.yml \
     -f infrastructure/docker/docker-compose.additions.phase8.yml \
     -f infrastructure/docker/docker-compose.additions.phase9.yml up -d
2) Generate some transfers (Phase 8 fast path or Payments gRPC).
3) Read balances at speed:
   BASE_URL=http://localhost:5181 k6 run infrastructure/devops/k6/balance-read-10k-rps.js
4) Verify cache path returns `source="cache"` after warmup.
