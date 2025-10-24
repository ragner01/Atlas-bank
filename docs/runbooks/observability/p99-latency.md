# P99 Latency Alert Runbook

## Alert: P99LatencyTooHigh

**Severity:** Page  
**Description:** P99 latency > 250ms for more than 10 minutes

## Immediate Actions (0-5 minutes)

1. **Check Grafana Dashboard**
   - Navigate to: https://grafana.atlasbank.com/d/limits-overview
   - Look for which service(s) are experiencing high latency
   - Check if it's a specific endpoint or service-wide

2. **Check Service Health**
   ```bash
   # Check service health endpoints
   curl http://paymentsapi:5191/health
   curl http://limits:5901/health
   curl http://trust:5801/health
   ```

3. **Check Resource Usage**
   ```bash
   # Check CPU and memory usage
   kubectl top pods -n atlasbank
   docker stats
   ```

## Investigation Steps (5-15 minutes)

1. **Check Application Logs**
   ```bash
   # Check for errors or slow queries
   kubectl logs -n atlasbank deployment/paymentsapi --tail=100
   kubectl logs -n atlasbank deployment/limits --tail=100
   ```

2. **Check Database Performance**
   ```bash
   # Check PostgreSQL connections and slow queries
   kubectl exec -n atlasbank postgres-0 -- psql -c "SELECT * FROM pg_stat_activity WHERE state = 'active';"
   ```

3. **Check Redis Performance**
   ```bash
   # Check Redis latency
   kubectl exec -n atlasbank redis-0 -- redis-cli --latency-history
   ```

## Resolution Steps (15-30 minutes)

### If Database Issues:
1. **Check Connection Pool**
   - Increase connection pool size if needed
   - Check for connection leaks

2. **Check Slow Queries**
   - Enable slow query logging
   - Optimize problematic queries

### If Redis Issues:
1. **Check Memory Usage**
   ```bash
   kubectl exec -n atlasbank redis-0 -- redis-cli info memory
   ```

2. **Check Key Expiration**
   ```bash
   kubectl exec -n atlasbank redis-0 -- redis-cli --scan --pattern "vel:*" | head -10
   ```

### If Application Issues:
1. **Check Garbage Collection**
   - Monitor GC pressure
   - Consider increasing heap size

2. **Check Thread Pool**
   - Monitor thread pool exhaustion
   - Check for deadlocks

## Escalation

If latency remains high after 30 minutes:
1. **Page On-Call Engineer**
2. **Check for Infrastructure Issues**
3. **Consider Rolling Restart**

## Prevention

1. **Set up Performance Baselines**
2. **Implement Circuit Breakers**
3. **Add Request Timeouts**
4. **Monitor Resource Usage**
5. **Regular Performance Testing**

## Related Documentation

- [Performance Monitoring Guide](https://docs.atlasbank.com/observability/performance)
- [Database Optimization](https://docs.atlasbank.com/database/optimization)
- [Redis Best Practices](https://docs.atlasbank.com/cache/redis)