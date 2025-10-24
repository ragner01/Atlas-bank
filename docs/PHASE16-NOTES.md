# Phase 16 — Merchant Acquiring: Fees, Settlement, Recon, Webhooks
## What you got
- **Fees**: live quotes for MDR/scheme/fixed fees; env-tunable.
- **Settlement Worker**: groups daily merchant credits → writes `settlement/payout_YYYYMMDD.csv` to Blob; marks `settlements` for idempotency.
- **Reconciliation stub**: accepts scheme/network CSV settlement file uploads for later automated matching.
- **Webhooks**: HMAC-signed events with retry queue (`wh:q`, `wh:retry`).

## Next hardening
- Per-merchant fee tables & effective-date versions.
- Net settlement postings to merchant bank (GL) + payout instructions.
- Recon auto-matcher (RRN/amount/currency + time window tolerance).
- Exponential backoff & DLQ for webhooks, plus signing secret rotation.
