# Smoke
1) Up with Phase 16:
   docker compose -f infrastructure/docker/docker-compose.yml \
     -f infrastructure/docker/docker-compose.additions.phase8.yml \
     -f infrastructure/docker/docker-compose.additions.phase9.yml \
     -f infrastructure/docker/docker-compose.additions.phase15.yml \
     -f infrastructure/docker/docker-compose.additions.phase16.yml up -d

2) After a card charge (Phase 15), notify merchant:
   curl -s -X POST "http://localhost:5191/payments/notify-merchant?merchantId=m-123&rrn=R12345&amountMinor=150000&currency=NGN"

3) Dispatch one webhook:
   curl -s -X POST http://localhost:5703/webhooks/dispatch-once

4) Check Blob `settlement/` for payout CSV after the hourly job, or temporarily force run by setting SETL_INTERVAL_MIN=1 and waiting a minute
