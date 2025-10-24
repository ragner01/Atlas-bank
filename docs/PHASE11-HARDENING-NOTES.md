# Phase 11 Hardening — Devices / IPs / Merchants & Rich Features
**What's new**
- Graph model now includes **Device**, **Ip**, **Merchant** nodes.
- Kafka **risk-events** stream ingested to maintain these relationships:
  - `(Account)-[:USES_DEVICE]->(Device)`
  - `(Account)-[:USES_IP {ts}]->(Ip)`
  - `(Account)-[:PAYS_TO {ts}]->(Merchant)`
- Feature vector (8 dims): amount, out-degree(src), in-degree(dst), edge frequency, shared device count, shared IP count, merchant in-degree, recency minutes.
- **HybridScorer**:
  - Uses **ONNX** if available (input name `features`, shape `[1,8]`).
  - Otherwise **heuristic**.
  - **Redis blacklists** (ip/device/merchant) hard-block immediately.

**Headers you can pass from clients to Payments**:
- `X-Device-Id`, `X-Ip`, `X-Merchant-Id`, `X-Customer-Id`

**Operational**:
- Topic `risk-events` complements `ledger-events`. Payments emits to it during pre-auth.
- You can build a simple admin UI to manage Redis blacklists (future).
