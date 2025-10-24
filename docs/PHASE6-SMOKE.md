# Phase 6 Smoke
1) Ensure ClickHouse table `ledger_events` exists (see compose note).
2) `make up` to start services; confirm `riskfeatures` on :5301.
3) Generate transfers to emit ledger events; AML worker now calls Feature Service, compares totals against `aml-rules.phase6.yaml`, and opens cases if thresholds are exceeded.
4) Set OAuth env for Gateway and hit routes with a JWT carrying scopes (`accounts.read`, `payments.write`, `aml.read`). Without scopes, requests should be 403. Over rate limit returns 429.
