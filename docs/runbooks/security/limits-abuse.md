# Limits Abuse Alert Runbook

## Alert: LimitsSoftReviewSpike / LimitsHardBlockSpike

**Severity:** Warning / Page  
**Description:** Spike in limits checks or hard blocks indicating possible abuse or attack

## Immediate Actions (0-5 minutes)

1. **Check Limits Dashboard**
   - Navigate to: https://grafana.atlasbank.com/d/limits-overview
   - Check limits checks per minute
   - Identify which limits are being triggered

2. **Check Recent Activity**
   ```bash
   # Check recent limits checks
   kubectl logs -n atlasbank deployment/limits --tail=100 | grep -i "check\|block"
   ```

3. **Check IP Patterns**
   ```bash
   # Check for suspicious IP patterns
   kubectl logs -n atlasbank deployment/limits --tail=1000 | grep -o "Ip=[0-9.]*" | sort | uniq -c | sort -nr
   ```

## Investigation Steps (5-15 minutes)

1. **Analyze Request Patterns**
   ```bash
   # Check for rapid-fire requests
   kubectl logs -n atlasbank deployment/limits --tail=1000 | grep -E "ActorId|DeviceId|MerchantId" | head -20
   ```

2. **Check Geographic Patterns**
   ```bash
   # Check for unusual geographic patterns
   kubectl logs -n atlasbank deployment/limits --tail=1000 | grep -E "Lat|Lng" | head -20
   ```

3. **Check Merchant Patterns**
   ```bash
   # Check for merchant-specific abuse
   kubectl logs -n atlasbank deployment/limits --tail=1000 | grep -o "MerchantId=[^,]*" | sort | uniq -c | sort -nr
   ```

## Resolution Steps (15-30 minutes)

### If Soft Review Spike:
1. **Check Velocity Rules**
   ```bash
   # Check current velocity limits
   curl http://limits:5901/limits/policy | jq '.velocity'
   ```

2. **Adjust Limits if Needed**
   ```bash
   # Temporarily tighten limits for specific actors
   curl -X POST http://limits:5901/limits/policy -H "Content-Type: application/json" -d '{
     "version": "1.0",
     "velocity": [
       {"id": "per_actor_1h_50k", "scope": "per_actor", "window": "1h", "currency": "NGN", "maxMinor": 5000000}
     ]
   }'
   ```

### If Hard Block Spike:
1. **Check Blocked Entities**
   ```bash
   # Check what entities are being blocked
   kubectl logs -n atlasbank deployment/limits --tail=1000 | grep "HARD_BLOCK" | head -20
   ```

2. **Check for Attack Patterns**
   ```bash
   # Look for coordinated attacks
   kubectl logs -n atlasbank deployment/limits --tail=1000 | grep -E "MCC|Time|Geo" | head -20
   ```

3. **Implement Temporary Blocks**
   ```bash
   # Block suspicious IPs temporarily
   curl -X POST http://limits:5901/limits/policy -H "Content-Type: application/json" -d '{
     "version": "1.0",
     "geo": [
       {"id": "block_suspicious_ip", "allow": false, "polygon": ["6.4654,3.4064","6.4660,3.4100","6.4620,3.4105","6.4615,3.4055"]}
     ]
   }'
   ```

## Escalation

If abuse continues after 30 minutes:
1. **Page Security Team**
2. **Implement IP Blocking**
3. **Contact Law Enforcement if Necessary**

## Prevention

1. **Implement Rate Limiting**
2. **Add CAPTCHA for Suspicious Activity**
3. **Monitor for Bot Patterns**
4. **Implement Device Fingerprinting**
5. **Regular Security Audits**

## Related Documentation

- [Security Monitoring Guide](https://docs.atlasbank.com/security/monitoring)
- [Fraud Prevention](https://docs.atlasbank.com/security/fraud)
- [Incident Response](https://docs.atlasbank.com/security/incident-response)