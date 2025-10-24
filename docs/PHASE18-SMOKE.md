# Phase 18 Smoke Tests

## Prerequisites
1. Ensure Phase 17 (Trust Core) is running
2. Ensure PostgreSQL is running with ledger data
3. Ensure Azure Blob Storage is accessible (or use development storage)

## 1. Start Phase 18 Services
```bash
docker compose -f infrastructure/docker/docker-compose.yml \
  -f infrastructure/docker/docker-compose.additions.phase18.yml up -d
```

## 2. Test Public Portal
```bash
# Open portal in browser
open http://localhost:5802/portal

# Or test with curl
curl -s http://localhost:5802/portal | grep -o "AtlasBank Trust Portal"
```

## 3. Test Trust Score Lookup
```bash
# Test with a sample entity ID
curl -s "http://localhost:5802/portal" | grep -o "check()"
```

## 4. Test SVG Badge Generation
```bash
# Test badge generation
curl -s "http://localhost:5802/badge/m-123.svg" | grep -o "Atlas Trust"

# Test with different entity IDs
curl -s "http://localhost:5802/badge/acc_abc.svg" | grep -o "Trust"
```

## 5. Test Regulator API
```bash
# Test with valid API key
curl -s -H "X-API-Key: dev-reg-key" \
  "http://localhost:5802/regulator/v1/entities/m-123/trust" | jq .

# Test without API key (should return 401)
curl -s "http://localhost:5802/regulator/v1/entities/m-123/trust" | grep -o "Unauthorized"
```

## 6. Test Open Data Endpoints
```bash
# Test index endpoint
curl -s "http://localhost:5802/opendata/index.json" | jq .

# Test specific data file (after export runs)
curl -s "http://localhost:5802/opendata/trust_week_20241201.csv" | head -5
```

## 7. Test Rate Limiting
```bash
# Test rate limiting (should handle 200 requests/second)
for i in {1..10}; do
  curl -s "http://localhost:5802/badge/test$i.svg" > /dev/null &
done
wait
echo "Rate limiting test completed"
```

## 8. Test Error Handling
```bash
# Test with invalid entity ID
curl -s "http://localhost:5802/badge/invalid.svg" | grep -o "UNKNOWN"

# Test with missing file
curl -s "http://localhost:5802/opendata/nonexistent.csv" | grep -o "Not Found"
```

## 9. Test Security Headers
```bash
# Check security headers
curl -I "http://localhost:5802/portal" | grep -E "(X-Content-Type-Options|X-Frame-Options|X-XSS-Protection)"
```

## 10. Test Trust Export Worker
```bash
# Check if export worker is running
docker logs trust-export | grep -o "Export completed"

# Force export by changing interval (optional)
docker exec trust-export sh -c "export EXPORT_EVERY_MIN=1"
```

## Expected Results
- ✅ Portal loads with trust score lookup form
- ✅ SVG badges generate with correct colors and scores
- ✅ Regulator API requires authentication
- ✅ Open data endpoints return proper JSON/CSV
- ✅ Rate limiting prevents abuse
- ✅ Error handling works correctly
- ✅ Security headers are present
- ✅ Export worker processes data correctly

## Troubleshooting
- **Portal not loading**: Check if Trust Core service is running
- **Badge generation fails**: Verify entity IDs exist in Trust Core
- **Regulator API fails**: Check API key configuration
- **Open data empty**: Wait for export worker to run or check database connection
- **Rate limiting issues**: Check Redis connection

## Performance Notes
- Badge SVGs should load in <100ms
- Portal should load in <500ms
- Regulator API should respond in <200ms
- Export worker should complete in <30 seconds

