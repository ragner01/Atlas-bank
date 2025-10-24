# Phase 23 — Lakehouse / BI / Feature Store

## Overview
Phase 23 introduces comprehensive analytics capabilities to AtlasBank, including a Parquet data lake, ClickHouse metrics ingestion, and a feature store API. This provides both operational analytics and deep historical analysis without requiring heavy infrastructure.

## What You Got

### 1. Parquet Data Lake (`Atlas.Lake.Etl`)
- **Kafka `ledger-events` → Partitioned Parquet files** in blob storage
- **Partitioned paths**: `lake/ledger/dt=YYYY-MM-DD/hour=HH/*.parquet`
- **Cheap, open, analytics-ready** format
- **Batch processing** with configurable row counts and time windows
- **Structured logging** with Serilog integration

### 2. ClickHouse Metrics (`Atlas.Metrics.CH`)
- **Streaming insert** into `tx_events` table using JSONEachRow format
- **Near-real-time dashboards** and ad-hoc SQL queries
- **Optimized schema** with partitioning by date and ordering by tenant/timestamp
- **Batch ingestion** for performance

### 3. Feature Store API (`Atlas.FeatureStore`)
- **`/features/txn`** → Rolling sums (10m/1h/1d), pair frequency, recency
- **`/features/observe`** → Publish observations to keep counters fresh
- **`/features/velocity`** → Velocity metrics for specific accounts and time windows
- **`/features/pair`** → Pair-specific features (count, recency)
- **`/features/stats`** → Overall feature store statistics
- **Redis-based** feature storage with TTL management

### 4. dbt Analytics Models
- **Staging models**: `stg_tx_events.sql` for data preparation
- **Fact models**: Daily volume, active accounts, transaction summaries
- **Dimension models**: Merchant attributes, account features
- **Data quality tests** and documentation
- **ClickHouse and DuckDB compatible**

## Architecture Benefits

### Clean Separation
- **Hot path untouched** - analytics pull from Kafka topics, not databases
- **Operational analytics** (ClickHouse) for real-time dashboards
- **Deep historical** (Parquet) for long-term analysis and ML
- **Feature store** for ML/risk model consumption

### Cost Efficiency
- **Parquet compression** reduces storage costs
- **Partitioned storage** enables efficient querying
- **ClickHouse** provides fast analytics without expensive OLAP systems
- **Redis** provides fast feature lookups

### Scalability
- **Kafka-based** event streaming for high throughput
- **Batch processing** with configurable parameters
- **Horizontal scaling** of analytics services
- **Cloud-native** blob storage integration

## Technical Implementation

### Data Flow
```
Kafka (ledger-events) 
    ├── Atlas.Lake.Etl → Parquet files (blob storage)
    ├── Atlas.Metrics.CH → ClickHouse (tx_events)
    └── Atlas.FeatureStore → Redis (feature counters)
```

### Feature Store Design
- **Velocity counters**: 10m, 1h, 1d rolling sums with TTL
- **Pair counters**: Transaction count and last transaction timestamp
- **Account features**: Sender/receiver statistics
- **Merchant features**: Volume, customer count, activity patterns

### Analytics Models
- **Staging**: Data cleaning and type extraction
- **Facts**: Aggregated metrics by time, tenant, currency
- **Dimensions**: Entity attributes and classifications
- **Quality**: Data validation and freshness checks

## Configuration

### Environment Variables
```bash
# Lake ETL
KAFKA_BOOTSTRAP=redpanda:9092
TOPIC_LEDGER=ledger-events
BLOB_CONN=UseDevelopmentStorage=true
LAKE_CONTAINER=lake
LAKE_BATCH_ROWS=5000
LAKE_BATCH_SECONDS=15

# ClickHouse Metrics
CLICKHOUSE_HTTP=http://clickhouse:8123
CH_BATCH_ROWS=5000
CH_BATCH_SECONDS=5

# Feature Store
REDIS=redis:6379
CLICKHOUSE_CONN=Host=clickhouse;Port=9000;Username=default;Password=
```

### Docker Compose
- **lake-etl**: Parquet data lake service
- **metrics-ch**: ClickHouse ingestion service
- **featurestore**: Feature store API service
- **Health checks** and resource limits configured

## Usage Examples

### Feature Store API
```bash
# Get transaction features
curl "http://localhost:5831/features/txn?tenant=tnt_demo&src=acc_A&dst=acc_B&currency=NGN"

# Observe transaction for feature updates
curl -X POST "http://localhost:5831/features/observe" \
  -H "Content-Type: application/json" \
  -d '{"tenant":"tnt_demo","src":"acc_A","dst":"acc_B","minor":5000}'

# Get velocity features
curl "http://localhost:5831/features/velocity?tenant=tnt_demo&account=acc_A&window=1h"
```

### ClickHouse Queries
```sql
-- Daily volume by tenant
SELECT 
  toDate(fromUnixTimestamp64Milli(ts_ms)) as dt,
  tenant,
  currency,
  sum(minor) as volume_minor,
  count(*) as tx_count
FROM tx_events
GROUP BY dt, tenant, currency
ORDER BY dt DESC;

-- Active accounts per day
SELECT 
  toDate(fromUnixTimestamp64Milli(ts_ms)) as dt,
  tenant,
  count(distinct src) as active_senders,
  count(distinct dst) as active_receivers
FROM tx_events
GROUP BY dt, tenant
ORDER BY dt DESC;
```

### dbt Models
```bash
# Run all models
dbt run

# Run specific model
dbt run --models fct_daily_volume

# Test data quality
dbt test

# Generate documentation
dbt docs generate
```

## Why This Matters

### Business Value
- **Real-time insights** into transaction patterns and volumes
- **Risk model features** for fraud detection and AML
- **Operational dashboards** for business monitoring
- **Historical analysis** for trend identification and reporting

### Technical Value
- **Feature parity** with larger fintech players
- **Simpler, cheaper, faster** than traditional data warehouses
- **Cloud-native** architecture with auto-scaling
- **Open standards** (Parquet, SQL) for vendor independence

### Compliance Value
- **Audit trails** with immutable Parquet files
- **Data lineage** from source to analytics
- **Retention policies** with partitioned storage
- **Data quality** monitoring and alerting

## Next Steps

### Immediate Enhancements
1. **Move `/features/observe` behind Kafka consumer** to compute features from streams only
2. **Add Materialized Views** in ClickHouse for minute/hour/day rollups
3. **Implement Delta/Iceberg** table format for ACID updates
4. **Add dbt CI** on PRs with data tests

### Advanced Features
1. **Real-time dashboards** with Grafana and ClickHouse
2. **ML feature pipelines** with automated feature engineering
3. **Data quality monitoring** with anomaly detection
4. **Cost optimization** with intelligent partitioning and compression

### Production Readiness
1. **Monitoring and alerting** for all analytics services
2. **Backup and disaster recovery** for Parquet files
3. **Performance tuning** for ClickHouse and Redis
4. **Security hardening** with encryption and access controls

## Integration Points

### Risk & AML (Phase 11/19)
- **Feature store** provides velocity and pattern features
- **ClickHouse** enables real-time risk scoring
- **Parquet** supports historical risk analysis

### Limits & Controls (Phase 19)
- **Velocity features** for limit enforcement
- **Real-time metrics** for limit monitoring
- **Historical patterns** for limit optimization

### Compliance & Reporting
- **Audit trails** in Parquet files
- **Regulatory reporting** with dbt models
- **Data quality** monitoring and validation

This phase establishes AtlasBank as a data-driven organization with comprehensive analytics capabilities that scale with business growth while maintaining cost efficiency and operational simplicity.
