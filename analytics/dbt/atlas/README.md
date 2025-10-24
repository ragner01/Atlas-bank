# dbt Skeleton (DuckDB/ClickHouse friendly)

This dbt project provides analytics models for AtlasBank transaction data. It's designed to work with both ClickHouse and DuckDB adapters.

## Models Overview

### Staging Models
- `stg_tx_events.sql` — read ClickHouse `tx_events` or Parquet files

### Fact Models
- `fct_daily_volume.sql` — daily volume by tenant/currency
- `fct_active_accounts.sql` — distinct active senders/receivers per day
- `fct_transaction_summary.sql` — transaction counts and amounts by various dimensions

### Dimension Models
- `dim_merchants.sql` — derive merchant attributes (fee tier, trust band)
- `dim_accounts.sql` — account-level aggregations and features

## Usage

### With ClickHouse
```bash
dbt run --target clickhouse
dbt test --target clickhouse
```

### With DuckDB
```bash
dbt run --target duckdb
dbt test --target duckdb
```

## Configuration

Update `dbt_project.yml` with your database connection details:

```yaml
name: 'atlas'
version: '1.0.0'
config-version: 2

model-paths: ["models"]
analysis-paths: ["analysis"]
test-paths: ["tests"]
seed-paths: ["seeds"]
macro-paths: ["macros"]
snapshot-paths: ["snapshots"]

target-path: "target"
clean-targets:
  - "target"
  - "dbt_packages"

models:
  atlas:
    staging:
      +materialized: view
    marts:
      +materialized: table
```

## Data Quality Tests

The project includes data quality tests to ensure:
- Row count sanity checks
- Referential integrity
- Data freshness
- Value range validations

## Features

- **Incremental models** for large datasets
- **Data quality tests** with dbt-expectations
- **Documentation** with column descriptions
- **Lineage** visualization
- **CI/CD ready** with GitHub Actions
