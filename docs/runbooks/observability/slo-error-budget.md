# API SLO Error Budget Burn Runbook

## Alert: APISLOErrorBudgetBurn

**Severity:** Page  
**Description:** Error budget burn rate >0.5% for more than 10 minutes

## Immediate Actions (0-5 minutes)

1. **Check Error Rate Dashboard**
   - Navigate to: https://grafana.atlasbank.com/d/limits-overview
   - Identify which service(s) are experiencing errors
   - Check error rate trends

2. **Check Service Health**
   ```bash
   # Check service health endpoints
   curl http://paymentsapi:5191/health
   curl http://limits:5901/health
   curl http://trust:5801/health
   ```

3. **Check Recent Deployments**
   ```bash
   # Check if recent deployments caused issues
   kubectl get deployments -n atlasbank
   kubectl rollout history deployment/paymentsapi -n atlasbank
   ```

## Investigation Steps (5-15 minutes)

1. **Check Application Logs**
   ```bash
   # Check for errors in logs
   kubectl logs -n atlasbank deployment/paymentsapi --tail=100 | grep -i error
   kubectl logs -n atlasbank deployment/limits --tail=100 | grep -i error
   ```

2. **Check Error Types**
   ```bash
   # Check specific error codes
   kubectl logs -n atlasbank deployment/paymentsapi --tail=1000 | grep -E "(4[0-9][0-9]|5[0-9][0-9])"
   ```

3. **Check Database Connectivity**
   ```bash
   # Check database connection errors
   kubectl logs -n atlasbank deployment/paymentsapi --tail=100 | grep -i "database\|connection\|timeout"
   ```

## Resolution Steps (15-30 minutes)

### If Database Issues:
1. **Check Connection Pool**
   ```bash
   # Check connection pool status
   kubectl exec -n atlasbank postgres-0 -- psql -c "SELECT * FROM pg_stat_activity;"
   ```

2. **Check Database Locks**
   ```bash
   # Check for blocking queries
   kubectl exec -n atlasbank postgres-0 -- psql -c "SELECT * FROM pg_locks WHERE NOT granted;"
   ```

### If Redis Issues:
1. **Check Redis Connectivity**
   ```bash
   # Check Redis connection
   kubectl exec -n atlasbank redis-0 -- redis-cli ping
   ```

2. **Check Redis Memory**
   ```bash
   # Check Redis memory usage
   kubectl exec -n atlasbank redis-0 -- redis-cli info memory
   ```

### If Application Issues:
1. **Check Resource Limits**
   ```bash
   # Check if pods are hitting resource limits
   kubectl describe pods -n atlasbank | grep -A 5 "Limits\|Requests"
   ```

2. **Check for Memory Issues**
   ```bash
   # Check for OOM kills
   kubectl get events -n atlasbank | grep -i "oom\|killed"
   ```

## Escalation

If error rate remains high after 30 minutes:
1. **Page On-Call Engineer**
2. **Consider Rolling Back Recent Deployment**
3. **Check for Infrastructure Issues**

## Prevention

1. **Implement Health Checks**
2. **Add Circuit Breakers**
3. **Monitor Resource Usage**
4. **Regular Load Testing**
5. **Implement Graceful Degradation**

## Related Documentation

- [Error Handling Guide](https://docs.atlasbank.com/observability/errors)
- [Database Troubleshooting](https://docs.atlasbank.com/database/troubleshooting)
- [Redis Troubleshooting](https://docs.atlasbank.com/cache/troubleshooting)