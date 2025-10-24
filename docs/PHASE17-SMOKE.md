# Smoke
1) Bring up stack with Phase 17:
   docker compose -f infrastructure/docker/docker-compose.yml \
     -f infrastructure/docker/docker-compose.additions.phase17.yml up -d

2) Add feedback:
   curl -X POST "http://localhost:5801/trust/feedback?from=u-1&to=m-123&rating=5"

3) Query trust score:
   curl "http://localhost:5801/trust/score?entityId=m-123"

4) Get transparency digest (hash anchor):
   curl "http://localhost:5801/trust/transparency/digest"

5) Verify proof for a known audit seq/hash:
   curl -X POST http://localhost:5801/trust/proof -H 'Content-Type: application/json' -d '{"seq":10,"hashHex":"ABC..."}'
