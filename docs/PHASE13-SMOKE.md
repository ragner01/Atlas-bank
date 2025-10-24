# Smoke
1) Up the stack including Phase 12 + 13:
   docker compose -f infrastructure/docker/docker-compose.yml \
     -f infrastructure/docker/docker-compose.additions.phase8.yml \
     -f infrastructure/docker/docker-compose.additions.phase9.yml \
     -f infrastructure/docker/docker-compose.additions.phase12.yml \
     -f infrastructure/docker/docker-compose.additions.phase13.yml up -d

2) Generate transfers on both regions to induce small drift:
   curl -s -X POST 'http://localhost:6181/ledger/fast-transfer?sourceAccountId=acc_X&destinationAccountId=acc_Y&minor=10000&currency=NGN&narration=A&tenantId=tnt_demo' -H 'Idempotency-Key: a1'
   curl -s -X POST 'http://localhost:7181/ledger/fast-transfer?sourceAccountId=acc_X&destinationAccountId=acc_Y&minor=7000&currency=NGN&narration=B&tenantId=tnt_demo' -H 'Idempotency-Key: b1'

3) Wait for Debezium → watermarks. Check:
   docker logs consistency-wm --tail=50
   # confirm wm:{tenant}:global advances (use redis-cli if desired)

4) Auto-heal should post a compensating entry on the smaller region within ~10s:
   docker logs autoheal --tail=200 | grep "Auto-heal applied"

5) RYW test:
   # capture a ts (approximate): now=$(date +%s%3N)
   curl -s "http://localhost:6181/ledger/accounts/acc_Y/balance/global?currency=NGN&afterTsMs=$now" -H "X-Tenant-Id: tnt_demo"

6) Verify drift hash is near zero:
   redis-cli HGETALL drift:tnt_demo:acc_Y:NGN
