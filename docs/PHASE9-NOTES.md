# Phase 9 — Read-Model Cache + Hedged Reads
## What you got
- **Kafka → Redis projection** updates per-account balances at **write time** (near-real-time).
- **Key format:** `balance:{tenant}:{account}:{currency}` as HASH { minor, v, ts } with short TTL.
- **Hedged read endpoint** returns the **first** of cache or DB (tiny 12ms hedge). On cache miss, DB value is returned and **backfilled**.
- **Watermarking:** projection uses **Kafka offset** as version `v`. You can add strict checks by comparing producer partition/offset if carried on requests.

## SLO intent
- Hot reads should hit cache → **p99 < 50ms**.
- Cache miss gracefully falls back to DB; background projector catches up.

## Next hardening
- Promote hashes to **RedisJSON** with schema & TS index (if using Redis Stack).
- Add **per-partition checkpoints** to avoid stale writes across rebalances.
- Implement **dual-read verify** path behind a 1% feature flag to detect drift and auto-heal.
