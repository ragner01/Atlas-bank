# Phase 26 — Cost Autotuning + Real-time Optimization

## Overview
Phase 26 introduces real-time balance updates, intelligent caching, load shedding, and autoscaling capabilities to optimize costs and performance during traffic spikes.

## What You Get

### 🔄 **Realtime Service (SignalR/WebSocket)**
- **Atlas.Realtime**: Broadcasts balance updates from Kafka topic `balance-updates`
- **WebSocket Hub**: Clients subscribe to account-specific balance updates
- **Auto-reconnection**: Mobile apps automatically reconnect on connection loss
- **Connection Management**: Efficient group-based subscriptions

### 💾 **Balance Caching with Precise Invalidation**
- **Redis Short-TTL Cache**: 5-second cache with instant invalidation
- **Pub/Sub Invalidation**: `balance:invalidate` channel for precise cache clearing
- **60-90% DB Call Reduction**: During traffic spikes
- **Cache Statistics**: Real-time monitoring of cache performance

### 🛡️ **Load Shedding on Payments API**
- **Token Bucket Algorithm**: Rate limiting with burst capacity
- **Graceful Degradation**: Returns `503 Retry-After` instead of cascading failures
- **Configurable Limits**: `LS_RATE_PER_SEC=400`, `LS_BURST=800`
- **Load Statistics**: Real-time monitoring of shedding metrics

### 📈 **Autoscaling Blueprints**
- **HPA (CPU-driven)**: For stateless APIs (Payments, Ledger, Realtime)
- **KEDA (Kafka lag)**: For workers (Settlement, Fees, Recon)
- **VPA (Memory optimization)**: Automatic resource tuning
- **PodDisruptionBudgets**: Ensure availability during scaling

### 📱 **Mobile App Real-time Updates**
- **Live Balance**: Instant balance updates without manual refresh
- **Connection Status**: Visual indicator of real-time connection
- **Offline Handling**: Graceful fallback to cached data
- **WebSocket Management**: Automatic reconnection and error handling

## Cost/Performance Impact

### 💰 **Cost Optimization**
- **60-90% fewer balance DB calls** during spikes (cache + invalidation)
- **Right-sized compute**: Scale out only when needed (HPA/KEDA)
- **Resource efficiency**: VPA optimizes memory allocation
- **Reduced timeouts**: Load shedding prevents cascading failures

### ⚡ **Performance Improvements**
- **Smooth tails**: Fewer timeouts when traffic surges
- **Instant UI updates**: Real-time balance changes
- **Better UX**: No manual refresh needed
- **Reduced latency**: Cached responses for hot paths

## Architecture Components

### Services
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Mobile App    │    │   Web Console   │    │   API Gateway   │
│   (WebSocket)   │    │   (WebSocket)   │    │                 │
└─────────┬───────┘    └─────────┬───────┘    └─────────┬───────┘
          │                      │                      │
          └──────────────────────┼──────────────────────┘
                                 │
                    ┌─────────────▼─────────────┐
                    │     Atlas.Realtime        │
                    │     (SignalR Hub)         │
                    └─────────────┬─────────────┘
                                  │
                    ┌─────────────▼─────────────┐
                    │        Kafka              │
                    │   (balance-updates)       │
                    └─────────────┬─────────────┘
                                  │
                    ┌─────────────▼─────────────┐
                    │     Payments API          │
                    │   (Load Shedding)         │
                    └─────────────┬─────────────┘
                                  │
                    ┌─────────────▼─────────────┐
                    │      Ledger API           │
                    │   (Redis Cache)           │
                    └───────────────────────────┘
```

### Data Flow
1. **Transfer Initiated**: Payments API receives transfer request
2. **Load Shedding**: Token bucket checks if request can be processed
3. **Balance Update**: Ledger processes the transfer
4. **Cache Invalidation**: Redis pub/sub invalidates affected accounts
5. **Realtime Push**: Kafka publishes balance updates
6. **Client Update**: SignalR broadcasts to subscribed clients
7. **UI Refresh**: Mobile/web apps update balance instantly

## Configuration

### Environment Variables
```bash
# Realtime Service
KAFKA_BOOTSTRAP=redpanda:9092
TOPIC_BALANCE_UPDATES=balance-updates

# Payments API Load Shedding
LS_RATE_PER_SEC=400
LS_BURST=800

# Ledger API Caching
REDIS=redis:6379
LEDGER_INTERNAL=http://ledgercore:6182

# Mobile App
EXPO_PUBLIC_REALTIME_BASE=http://localhost:5851
EXPO_PUBLIC_LEDGER_BASE=http://localhost:6181
```

### Kubernetes Resources
- **HPA**: CPU/Memory-based scaling
- **KEDA**: Kafka lag-based scaling
- **VPA**: Memory optimization
- **PDB**: Availability guarantees

## Hardening Ideas

### 🚀 **Performance Optimizations**
- **Request-level caching**: E-Tag + If-None-Match for read-only queries
- **Feature store observations**: Move to background to trim hot-path time
- **Connection pooling**: Optimize WebSocket connections
- **Batch processing**: Group multiple balance updates

### 📊 **Monitoring & Alerting**
- **Autoscaler SLO**: Target p99 latency 200-250ms
- **Error budget burn**: <0.5% error rate
- **Cache hit ratio**: Monitor Redis performance
- **Load shedding metrics**: Track shedding frequency

### 🔒 **Security Enhancements**
- **WebSocket authentication**: JWT-based connection validation
- **Rate limiting**: Per-user connection limits
- **Input validation**: Sanitize WebSocket messages
- **Audit logging**: Track all real-time events

### 💾 **Data Consistency**
- **Event ordering**: Ensure balance updates are processed in order
- **Conflict resolution**: Handle concurrent updates
- **Idempotency**: Prevent duplicate balance updates
- **Backpressure**: Handle high-frequency updates

## Testing Strategy

### Unit Tests
- Token bucket algorithm
- Cache invalidation logic
- WebSocket message handling
- Load shedding thresholds

### Integration Tests
- End-to-end balance updates
- Cache consistency
- WebSocket reconnection
- Autoscaling triggers

### Load Tests
- High-frequency transfers
- Concurrent WebSocket connections
- Cache performance under load
- Load shedding effectiveness

## Deployment Notes

### Prerequisites
- Redis 7+ for caching
- Kafka/Redpanda for messaging
- Kubernetes cluster with HPA/KEDA support
- Load balancer with WebSocket support

### Rollout Strategy
1. Deploy Realtime service
2. Enable caching on Ledger API
3. Add load shedding to Payments API
4. Update mobile apps
5. Configure autoscaling
6. Monitor and tune

### Rollback Plan
- Disable load shedding
- Fall back to direct DB queries
- Disable real-time updates
- Scale down autoscaling

## Success Metrics

### Performance
- **P99 Latency**: <250ms for balance queries
- **Cache Hit Ratio**: >80% during peak traffic
- **Load Shedding**: <5% of requests shed
- **WebSocket Uptime**: >99.9%

### Cost
- **DB Call Reduction**: 60-90% during spikes
- **Resource Utilization**: Optimal CPU/Memory usage
- **Scaling Efficiency**: Right-sized instances
- **Error Rate**: <0.5% overall

### User Experience
- **Real-time Updates**: Instant balance changes
- **Connection Stability**: Minimal disconnections
- **Response Time**: Sub-second UI updates
- **Availability**: 99.9% uptime
