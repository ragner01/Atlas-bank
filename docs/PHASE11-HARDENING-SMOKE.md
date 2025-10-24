# Smoke
1) Up the stack with Phase 8/9/11 + hardening:
   docker compose -f infrastructure/docker/docker-compose.yml \
     -f infrastructure/docker/docker-compose.additions.phase8.yml \
     -f infrastructure/docker/docker-compose.additions.phase9.yml \
     -f infrastructure/docker/docker-compose.additions.phase11.yml \
     -f infrastructure/docker/docker-compose.additions.phase11h.yml up -d

2) Send a risk-scored transfer w/ context:
   curl -s -X POST http://localhost:5191/payments/transfers/with-risk \
     -H 'Content-Type: application/json' \
     -H 'X-Tenant-Id: tnt_demo' \
     -H 'X-Device-Id: dev-xyz' \
     -H 'X-Ip: 1.2.3.4' \
     -H 'X-Merchant-Id: m-123' \
     -d '{"SourceAccountId":"acc_A","DestinationAccountId":"acc_B","Minor":45000000,"Currency":"NGN","Narration":"pos"}'

3) (Optional) Add the IP to a blacklist and retry; expect BLOCK:
   docker exec -it <redis> redis-cli SADD risk:blacklist:ip 1.2.3.4

4) Inspect Neo4j (:7474) for nodes: `MATCH (n) RETURN labels(n), count(n);`
