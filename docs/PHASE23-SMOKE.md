# Phase 23 Smoke Tests

## Prerequisites
- Docker and Docker Compose installed
- AtlasBank Phase 23 services built and ready
- ClickHouse and Redis running
- Kafka/Redpanda running with ledger-events topic

## 1. Start Phase 23 Services

```bash
# Start all Phase 23 services
docker compose -f infrastructure/docker/docker-compose.yml \
  -f infrastructure/docker/docker-compose.additions.phase23.yml up -d

# Check service status
docker compose ps
```

Expected output:
```
NAME                IMAGE                    COMMAND                  SERVICE             CREATED             STATUS                    PORTS
atlas-lake-etl      atlas-lake-etl:latest   "dotnet Atlas.Lake.Etl.dll" lake-etl           2 minutes ago       Up 2 minutes (healthy)
atlas-metrics-ch    atlas-metrics-ch:latest "dotnet Atlas.Metrics.CH.dll" metrics-ch       2 minutes ago       Up 2 minutes (healthy)
atlas-featurestore  atlas-featurestore:latest "dotnet Atlas.FeatureStore.dll" featurestore 2 minutes ago       Up 2 minutes (healthy)
```

## 2. Generate Test Data

```bash
# Generate some test transactions to create ledger events
echo "Generating test transactions..."

# Create test accounts
curl -X POST "http://localhost:6181/ledger/accounts" \
  -H "Content-Type: application/json" \
  -d '{"accountId":"msisdn::2348100000001","tenantId":"tnt_demo","currency":"NGN"}'

curl -X POST "http://localhost:6181/ledger/accounts" \
  -H "Content-Type: application/json" \
  -d '{"accountId":"msisdn::2348100000002","tenantId":"tnt_demo","currency":"NGN"}'

# Create some test transfers to generate ledger events
for i in {1..10}; do
  curl -X POST "http://localhost:5191/payments/transfers/fast" \
    -H "Content-Type: application/json" \
    -d "{
      \"sourceAccountId\":\"msisdn::2348100000001\",
      \"destinationAccountId\":\"msisdn::2348100000002\",
      \"minor\":$((1000 + i * 100)),
      \"currency\":\"NGN\",
      \"narration\":\"Test transfer $i\"
    }"
  sleep 1
done

echo "Test transactions generated"
```

## 3. Verify ClickHouse Data Ingestion

```bash
# Wait for data to be ingested (allow 30 seconds)
echo "Waiting for ClickHouse ingestion..."
sleep 30

# Check ClickHouse table exists
echo "Checking ClickHouse table..."
curl "http://localhost:8123/?query=SHOW%20TABLES"

# Check row count
echo "Checking transaction count in ClickHouse..."
curl "http://localhost:8123/?query=SELECT%20count()%20FROM%20tx_events"

# Check recent transactions
echo "Checking recent transactions..."
curl "http://localhost:8123/?query=SELECT%20*%20FROM%20tx_events%20ORDER%20BY%20ts_ms%20DESC%20LIMIT%205"

# Check daily volume
echo "Checking daily volume..."
curl "http://localhost:8123/?query=SELECT%20toDate(fromUnixTimestamp64Milli(ts_ms))%20as%20dt,%20tenant,%20currency,%20sum(minor)%20as%20volume_minor,%20count()%20as%20tx_count%20FROM%20tx_events%20GROUP%20BY%20dt,%20tenant,%20currency%20ORDER%20BY%20dt%20DESC"
```

Expected output:
- Table `tx_events` should exist
- Row count should be > 0
- Recent transactions should show test data
- Daily volume should show aggregated data

## 4. Verify Feature Store API

```bash
# Test health endpoint
echo "Testing Feature Store health..."
curl "http://localhost:5831/health"

# Test transaction features
echo "Testing transaction features..."
curl "http://localhost:5831/features/txn?tenant=tnt_demo&src=msisdn::2348100000001&dst=msisdn::2348100000002&currency=NGN"

# Test velocity features
echo "Testing velocity features..."
curl "http://localhost:5831/features/velocity?tenant=tnt_demo&account=msisdn::2348100000001&window=1h"

# Test pair features
echo "Testing pair features..."
curl "http://localhost:5831/features/pair?tenant=tnt_demo&src=msisdn::2348100000001&dst=msisdn::2348100000002"

# Test feature store stats
echo "Testing feature store stats..."
curl "http://localhost:5831/features/stats"

# Test feature observation
echo "Testing feature observation..."
curl -X POST "http://localhost:5831/features/observe" \
  -H "Content-Type: application/json" \
  -d '{
    "tenant":"tnt_demo",
    "src":"msisdn::2348100000001",
    "dst":"msisdn::2348100000002",
    "minor":5000
  }'
```

Expected output:
- Health endpoint should return `{"ok":true}`
- Transaction features should return velocity and pair data
- Velocity features should return account-specific metrics
- Pair features should return transaction count and recency
- Stats should return Redis information
- Observation should return `202 Accepted`

## 5. Verify Parquet Data Lake

```bash
# Check if blob storage is accessible (using Azurite for local development)
echo "Checking Parquet data lake..."

# List containers (if using Azurite)
curl "http://localhost:10000/devstoreaccount1?comp=list"

# Check if lake container exists
curl "http://localhost:10000/devstoreaccount1/lake?comp=list"

# List Parquet files (if any exist)
curl "http://localhost:10000/devstoreaccount1/lake?comp=list&prefix=ledger/"
```

Expected output:
- Blob storage should be accessible
- `lake` container should exist
- Parquet files should be created in `ledger/dt=YYYY-MM-DD/hour=HH/` structure

## 6. Test Analytics Models (dbt)

```bash
# Navigate to dbt project
cd analytics/dbt/atlas

# Install dbt dependencies (if any)
# dbt deps

# Test connection to ClickHouse
# dbt debug --target clickhouse

# Run staging model
# dbt run --models stg_tx_events --target clickhouse

# Run fact models
# dbt run --models fct_daily_volume --target clickhouse
# dbt run --models fct_active_accounts --target clickhouse

# Run dimension models
# dbt run --models dim_merchants --target clickhouse

# Run tests
# dbt test --target clickhouse
```

Expected output:
- dbt should connect to ClickHouse successfully
- Models should run without errors
- Tests should pass

## 7. Performance Testing

```bash
# Test ClickHouse query performance
echo "Testing ClickHouse performance..."
time curl "http://localhost:8123/?query=SELECT%20count()%20FROM%20tx_events"

# Test Feature Store API performance
echo "Testing Feature Store performance..."
time curl "http://localhost:5831/features/txn?tenant=tnt_demo&src=msisdn::2348100000001&dst=msisdn::2348100000002&currency=NGN"

# Test concurrent requests
echo "Testing concurrent Feature Store requests..."
for i in {1..10}; do
  curl "http://localhost:5831/features/txn?tenant=tnt_demo&src=msisdn::2348100000001&dst=msisdn::2348100000002&currency=NGN" &
done
wait
```

Expected output:
- ClickHouse queries should complete in < 1 second
- Feature Store API should respond in < 100ms
- Concurrent requests should all succeed

## 8. Error Handling Tests

```bash
# Test invalid parameters
echo "Testing error handling..."

# Invalid tenant
curl "http://localhost:5831/features/txn?tenant=invalid&src=msisdn::2348100000001&dst=msisdn::2348100000002&currency=NGN"

# Invalid account format
curl "http://localhost:5831/features/txn?tenant=tnt_demo&src=invalid&dst=msisdn::2348100000002&currency=NGN"

# Invalid currency
curl "http://localhost:5831/features/txn?tenant=tnt_demo&src=msisdn::2348100000001&dst=msisdn::2348100000002&currency=INVALID"

# Invalid observation payload
curl -X POST "http://localhost:5831/features/observe" \
  -H "Content-Type: application/json" \
  -d '{"invalid":"payload"}'
```

Expected output:
- Invalid requests should return appropriate error codes
- Error messages should be descriptive
- Service should remain stable

## 9. Monitoring and Logs

```bash
# Check service logs
echo "Checking service logs..."

# Lake ETL logs
docker logs atlas-lake-etl --tail 20

# ClickHouse Metrics logs
docker logs atlas-metrics-ch --tail 20

# Feature Store logs
docker logs atlas-featurestore --tail 20

# Check health status
echo "Checking health status..."
curl "http://localhost:5831/health"
```

Expected output:
- Logs should show successful processing
- No error messages in logs
- Health checks should pass

## 10. Cleanup

```bash
# Stop Phase 23 services
docker compose -f infrastructure/docker/docker-compose.yml \
  -f infrastructure/docker/docker-compose.additions.phase23.yml down

# Remove test data (optional)
docker exec -it atlas-postgres psql -U atlas -d atlas_bank -c "DELETE FROM journal_entries WHERE narration LIKE 'Test transfer%';"
```

## Success Criteria

✅ **All services start successfully** and pass health checks  
✅ **ClickHouse ingestion** processes ledger events and creates tx_events table  
✅ **Feature Store API** responds to all endpoints with correct data  
✅ **Parquet data lake** creates partitioned files in blob storage  
✅ **dbt models** run successfully against ClickHouse  
✅ **Performance** meets expectations (< 1s for ClickHouse, < 100ms for API)  
✅ **Error handling** works correctly for invalid inputs  
✅ **Logging** provides adequate visibility into service operations  

## Troubleshooting

### Common Issues

1. **ClickHouse connection failed**
   - Check if ClickHouse is running: `docker ps | grep clickhouse`
   - Verify connection string in environment variables

2. **Feature Store Redis connection failed**
   - Check if Redis is running: `docker ps | grep redis`
   - Verify Redis connection string

3. **Parquet files not created**
   - Check if blob storage is accessible
   - Verify LAKE_CONTAINER environment variable
   - Check Lake ETL logs for errors

4. **dbt models fail**
   - Verify ClickHouse connection
   - Check if tx_events table exists and has data
   - Review dbt logs for specific errors

### Debug Commands

```bash
# Check service status
docker compose ps

# Check service logs
docker logs <service-name> --tail 50

# Check ClickHouse data
curl "http://localhost:8123/?query=SELECT%20count()%20FROM%20tx_events"

# Check Redis keys
docker exec -it atlas-redis redis-cli KEYS "*"

# Check blob storage
curl "http://localhost:10000/devstoreaccount1/lake?comp=list"
```

This smoke test validates that Phase 23 analytics services are working correctly and can process real transaction data from the AtlasBank system.
