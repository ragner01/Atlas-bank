# Phase 19: Limits & Controls + Observability SLOs/Alerts/Runbooks

## Overview

Phase 19 implements a comprehensive limits and controls system with policy-as-code, real-time enforcement, and observability with SLOs, alerts, and runbooks.

## Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Payments API  │───▶│   Limits API    │───▶│     Redis       │
│                 │    │                 │    │                 │
│ /cnp/charge/    │    │ /limits/check   │    │ Policy Storage  │
│ enforced        │    │ /limits/policy  │    │ Velocity Counters│
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Canary        │    │   Prometheus    │    │   Grafana       │
│   Service       │    │                 │    │                 │
│                 │    │ SLO Rules       │    │ Dashboards      │
│ Synthetic Tests │    │ Alerts          │    │ Monitoring      │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Alertmanager  │───▶│   Webhooks      │───▶│   On-Call        │
│                 │    │   Service       │    │   Engineer       │
│ Alert Routing   │    │                 │    │                 │
│ Escalation      │    │ Notifications   │    │ Response        │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Components

### 1. Limits Service (`Atlas.Limits`)

**Purpose:** Policy-as-code limits enforcement with real-time velocity tracking

**Features:**
- Policy management via JSON API
- Velocity limits (per-actor, per-device, per-merchant)
- MCC allow/deny rules
- Time window restrictions
- Geofence blocking
- Redis-based counters with TTL

**API Endpoints:**
- `GET /limits/policy` - Get current policy
- `POST /limits/policy` - Update policy
- `POST /limits/check` - Check limits for transaction

**Policy Format:**
```json
{
  "version": "1.0",
  "velocity": [
    {"id": "per_actor_1h_100k", "scope": "per_actor", "window": "1h", "currency": "NGN", "maxMinor": 10000000}
  ],
  "mcc": [
    {"id": "deny_cash_like", "allow": false, "mcc": ["4829", "6011"]}
  ],
  "time": [
    {"id": "night_block", "allow": false, "cron": "0-6", "tz": "Africa/Lagos"}
  ],
  "geo": [
    {"id": "block_hotspot", "allow": false, "polygon": ["6.4654,3.4064","6.4660,3.4100"]}
  ]
}
```

### 2. Payments API Integration

**Purpose:** Wrap existing endpoints with limits enforcement

**Implementation:**
- `EnforceAsync` wrapper function
- Calls Limits service before processing
- Returns `HARD_BLOCK` (403) or `SOFT_REVIEW` (202) with headers
- Maintains original functionality

**Headers Added:**
- `X-Limit-Review: true` - Indicates soft review
- `X-Limit-Reason: <reason>` - Reason for review

### 3. Canary Service (`Atlas.Canary`)

**Purpose:** Synthetic monitoring of enforced endpoints

**Features:**
- Continuous health checks
- End-to-end transaction testing
- Configurable test intervals
- Structured logging

**Configuration:**
- `PAYMENTS_BASE` - Payments API URL
- `TENANT` - Tenant ID for tests
- `CANARY_MS` - Test interval in milliseconds

### 4. Observability Stack

#### Prometheus
- **Purpose:** Metrics collection and SLO monitoring
- **Configuration:** `prometheus.yml` with scrape targets
- **Rules:** `atlas-slos.yml` with SLO and alert rules

#### Alertmanager
- **Purpose:** Alert routing and escalation
- **Configuration:** `alertmanager.yml` with webhook routing
- **Integration:** Reuses existing webhooks service

#### Grafana
- **Purpose:** Visualization and monitoring dashboards
- **Dashboard:** `limits-overview.json` with key metrics
- **Metrics:** Request rate, latency, error rate, limits usage

### 5. Runbooks

**Purpose:** Actionable documentation for incident response

**Runbooks Created:**
- `p99-latency.md` - High latency response
- `slo-error-budget.md` - Error rate response
- `limits-abuse.md` - Security incident response

## SLOs and Alerts

### Service Level Objectives

1. **API Availability:** 99.5% error budget
2. **Latency:** P99 < 250ms
3. **Limits Response:** < 100ms

### Alert Rules

1. **APISLOErrorBudgetBurn** - Error rate > 0.5%
2. **P99LatencyTooHigh** - P99 latency > 250ms
3. **LimitsSoftReviewSpike** - Soft reviews > 50/min
4. **LimitsHardBlockSpike** - Hard blocks > 10/min
5. **CanaryDown** - Synthetic monitoring failure

## Security Features

### Policy Enforcement
- Real-time velocity tracking
- Geographic restrictions
- Time-based controls
- MCC-based filtering

### Attack Detection
- Rapid-fire request detection
- Geographic anomaly detection
- Coordinated attack patterns
- Bot behavior identification

### Response Actions
- `HARD_BLOCK` - Immediate denial
- `SOFT_REVIEW` - Allow with review flag
- Temporary policy adjustments
- IP-based blocking

## Performance Considerations

### Redis Optimization
- Efficient key patterns for velocity tracking
- TTL-based cleanup
- Connection pooling
- Memory optimization

### API Performance
- Minimal latency overhead
- Efficient policy evaluation
- Cached policy retrieval
- Optimized Redis operations

## Monitoring and Alerting

### Key Metrics
- Limits checks per minute
- Soft reviews per minute
- Hard blocks per minute
- API response times
- Error rates

### Dashboards
- Limits overview dashboard
- Performance metrics
- Security alerts
- SLO tracking

### Alerting
- Real-time notifications
- Escalation procedures
- Runbook integration
- On-call routing

## Deployment

### Docker Compose
- All services containerized
- Proper networking
- Health checks
- Resource limits

### Configuration
- Environment-based config
- Policy management
- Monitoring setup
- Alert configuration

## Testing

### Smoke Tests
- Policy management
- Limits enforcement
- Canary monitoring
- Alert verification

### Load Testing
- Velocity limit testing
- High-volume scenarios
- Performance validation
- Stress testing

## Future Enhancements

### Advanced Features
- Machine learning-based limits
- Dynamic policy adjustment
- Risk-based scoring
- Behavioral analysis

### Integration
- External fraud services
- Risk scoring APIs
- Compliance reporting
- Audit trails

### Scalability
- Multi-region deployment
- Policy replication
- Performance optimization
- Capacity planning