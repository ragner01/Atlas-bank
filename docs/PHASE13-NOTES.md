# Phase 13 — Auto-Heal + Watermarks + RYW

## What you got
- **Debezium Watermark Tracker**: consumes `regionA.public.outbox` & `regionB.public.outbox`, updates:
  - `wm:{tenant}:regionA` = last ts_ms, `wm:{tenant}:regionB`, and `wm:{tenant}:global` = min(A,B).
- **Read-Your-Writes SLA**:
  - Client captures a write token `afterTsMs` (e.g., Debezium `ts_ms` or server commit time).
  - `/ledger/accounts/{id}/balance/global?afterTsMs=<ts>` waits until `wm:{tenant}:global ≥ ts` (default wait ~800 ms), then returns.
  - `/consistency/wait?tenant=...&afterTsMs=...` is a generic gate you can call before any globally consistent read.
- **Auto-Heal Reconciler**:
  - Reads drift aggregates (`drift:{tenant}:{account}:{ccy}`) from Phase 12.
  - When **global watermark is fresh** and **|diff| ≤ HEAL_MAX_ABS_MINOR**, posts a compensating **fast-transfer** on the **smaller region**, between `suspense` and the affected account.
  - **Idempotent** via a composite key `heal::<region>::...::<wm>`.
  - Logs actions and updates the drift hash to reflect the heal.

## Operational guidance
- Keep `HEAL_MAX_ABS_MINOR` conservative; large discrepancies should be handled under change management.
- Suspense account should map to your **GL suspense** and be reviewed daily.
- Tie RYW token to real Debezium `ts_ms` when available (Phase 12 connectors already include it); until then, server commit time is an acceptable approximation.

## Security & audit
- All auto-heals carry narration `auto-heal(regionX)` and idempotency keys; they will appear in GL extracts (Phase 10).
- Consider a **two-man rule** to enable auto-heal in production (feature flag + approvals).
