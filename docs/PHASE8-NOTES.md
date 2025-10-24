# Phase 8 — High-Performance Core
Targets OPay/PalmPay/Moniepoint-grade responsiveness via:
- **DB fast-path**: one round-trip `sp_idem_transfer_execute` + advisory locks to avoid deadlocks, SERIALIZABLE retries in client.
- **Lean gRPC/HTTP**: minimal allocations, pooled connections (`NpgsqlDataSource`), prepared statements by driver, idempotency at DB row.
- **Tuning**:
  - Postgres: larger `shared_buffers`, async commit off for dev (enable in prod or use `synchronous_commit=remote_apply` with replicas), WAL sizing.
  - Kafka/Redpanda: `linger.ms=2`, idempotent producer, lz4 compression, bigger batch.
  - Kestrel: HTTP/2 for gRPC; avoid per-request allocations.
- **Perf test**: k6 script at 5k RPS, p99 < 120ms.
- Next: shard by account prefix, partitioned tables, read-model cache in Redis, hedged reads for balances.
