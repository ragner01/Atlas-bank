# Atlas.Risk.Features
Exposes aggregate features (e.g., velocity sums) from ClickHouse for AML/risk rules.
Requires ClickHouse table `ledger_events(ts DateTime64, tenant String, source String, dest String, minor Int64, currency String)`.
