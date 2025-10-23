#!/bin/bash

# AtlasBank API Testing Script
# This script tests all the core functionality of AtlasBank

set -e

echo "🏦 AtlasBank API Testing Script"
echo "================================"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
LEDGER_URL="http://localhost:5181"
PAYMENTS_URL="http://localhost:5191"
GATEWAY_URL="http://localhost:5080"

# Test counter
TESTS_PASSED=0
TESTS_FAILED=0

# Function to print test results
print_result() {
    if [ $1 -eq 0 ]; then
        echo -e "${GREEN}✅ PASS${NC}: $2"
        ((TESTS_PASSED++))
    else
        echo -e "${RED}❌ FAIL${NC}: $2"
        ((TESTS_FAILED++))
    fi
}

# Function to test HTTP endpoint
test_endpoint() {
    local url=$1
    local expected_status=$2
    local test_name=$3
    
    echo -e "${BLUE}Testing:${NC} $test_name"
    echo "URL: $url"
    
    response=$(curl -s -o /dev/null -w "%{http_code}" "$url" || echo "000")
    
    if [ "$response" -eq "$expected_status" ]; then
        print_result 0 "$test_name"
    else
        print_result 1 "$test_name (Expected: $expected_status, Got: $response)"
    fi
    echo ""
}

# Function to test POST endpoint
test_post_endpoint() {
    local url=$1
    local data=$2
    local headers=$3
    local expected_status=$4
    local test_name=$5
    
    echo -e "${BLUE}Testing:${NC} $test_name"
    echo "URL: $url"
    echo "Data: $data"
    
    response=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$url" \
        -H "Content-Type: application/json" \
        $headers \
        -d "$data" || echo "000")
    
    if [ "$response" -eq "$expected_status" ]; then
        print_result 0 "$test_name"
    else
        print_result 1 "$test_name (Expected: $expected_status, Got: $response)"
    fi
    echo ""
}

echo -e "${YELLOW}Starting AtlasBank API Tests...${NC}"
echo ""

# Test 1: Health Checks
echo "🔍 Testing Health Endpoints"
echo "---------------------------"

test_endpoint "$LEDGER_URL/health" 200 "Ledger Service Health Check"
test_endpoint "$PAYMENTS_URL/health" 200 "Payments Service Health Check"
test_endpoint "$GATEWAY_URL/health" 200 "API Gateway Health Check"

# Test 2: Ledger Service Tests
echo "📊 Testing Ledger Service"
echo "-------------------------"

# Test journal entry posting
test_post_endpoint "$LEDGER_URL/ledger/entries" \
    '{
        "SourceAccountId": "acc_001",
        "DestinationAccountId": "acc_002",
        "Minor": 100000,
        "Currency": "NGN",
        "Narration": "Test journal entry"
    }' \
    "" \
    202 \
    "Post Journal Entry"

# Test account balance retrieval
test_endpoint "$LEDGER_URL/ledger/accounts/acc_001/balance" 200 "Get Account Balance"

# Test 3: Payments Service Tests
echo "💳 Testing Payments Service"
echo "---------------------------"

# Test payment transfer with idempotency
test_post_endpoint "$PAYMENTS_URL/payments/transfers" \
    '{
        "SourceAccountId": "acc_001",
        "DestinationAccountId": "acc_002",
        "Minor": 50000,
        "Currency": "NGN",
        "Narration": "Test payment transfer"
    }' \
    '-H "Idempotency-Key: test-payment-001"' \
    202 \
    "Payment Transfer with Idempotency"

# Test duplicate payment (should return 200 due to idempotency)
test_post_endpoint "$PAYMENTS_URL/payments/transfers" \
    '{
        "SourceAccountId": "acc_001",
        "DestinationAccountId": "acc_002",
        "Minor": 50000,
        "Currency": "NGN",
        "Narration": "Duplicate payment test"
    }' \
    '-H "Idempotency-Key: test-payment-001"' \
    200 \
    "Duplicate Payment (Idempotency Test)"

# Test payment without idempotency key (should fail)
test_post_endpoint "$PAYMENTS_URL/payments/transfers" \
    '{
        "SourceAccountId": "acc_001",
        "DestinationAccountId": "acc_002",
        "Minor": 25000,
        "Currency": "NGN",
        "Narration": "Payment without idempotency"
    }' \
    "" \
    400 \
    "Payment without Idempotency Key (Should Fail)"

# Test 4: API Gateway Tests
echo "🌐 Testing API Gateway"
echo "----------------------"

# Test routing through gateway
test_post_endpoint "$GATEWAY_URL/payments/transfers" \
    '{
        "SourceAccountId": "acc_001",
        "DestinationAccountId": "acc_002",
        "Minor": 75000,
        "Currency": "NGN",
        "Narration": "Gateway test payment"
    }' \
    '-H "Idempotency-Key: gateway-test-001" -H "X-Tenant-Id: tnt_demo"' \
    202 \
    "Payment through API Gateway"

# Test 5: AML Monitoring Test
echo "🛡️ Testing AML Monitoring"
echo "------------------------"

# Test high-value transaction that should trigger AML
test_post_endpoint "$PAYMENTS_URL/payments/transfers" \
    '{
        "SourceAccountId": "acc_001",
        "DestinationAccountId": "acc_002",
        "Minor": 75000000,
        "Currency": "NGN",
        "Narration": "High-value transaction for AML testing"
    }' \
    '-H "Idempotency-Key: aml-test-001"' \
    202 \
    "High-Value Transaction (AML Trigger)"

echo "⏳ Waiting for AML processing..."
sleep 5

# Check AML worker logs
echo "Checking AML worker logs for alerts..."
if docker logs amlworker 2>&1 | grep -q "AML alert"; then
    print_result 0 "AML Alert Generated"
else
    print_result 1 "AML Alert Not Generated"
fi

# Test 6: Error Handling Tests
echo "🚨 Testing Error Handling"
echo "-------------------------"

# Test invalid currency
test_post_endpoint "$PAYMENTS_URL/payments/transfers" \
    '{
        "SourceAccountId": "acc_001",
        "DestinationAccountId": "acc_002",
        "Minor": 10000,
        "Currency": "INVALID",
        "Narration": "Invalid currency test"
    }' \
    '-H "Idempotency-Key: error-test-001"' \
    400 \
    "Invalid Currency (Should Fail)"

# Test negative amount
test_post_endpoint "$PAYMENTS_URL/payments/transfers" \
    '{
        "SourceAccountId": "acc_001",
        "DestinationAccountId": "acc_002",
        "Minor": -1000,
        "Currency": "NGN",
        "Narration": "Negative amount test"
    }' \
    '-H "Idempotency-Key: error-test-002"' \
    400 \
    "Negative Amount (Should Fail)"

# Test 7: Performance Tests
echo "⚡ Testing Performance"
echo "---------------------"

echo "Running basic performance test..."
start_time=$(date +%s.%N)

for i in {1..10}; do
    curl -s -X POST "$PAYMENTS_URL/payments/transfers" \
        -H "Content-Type: application/json" \
        -H "Idempotency-Key: perf-test-$i" \
        -d '{
            "SourceAccountId": "acc_001",
            "DestinationAccountId": "acc_002",
            "Minor": 1000,
            "Currency": "NGN",
            "Narration": "Performance test"
        }' > /dev/null
done

end_time=$(date +%s.%N)
duration=$(echo "$end_time - $start_time" | bc)

if (( $(echo "$duration < 5.0" | bc -l) )); then
    print_result 0 "Performance Test (10 requests in ${duration}s)"
else
    print_result 1 "Performance Test (10 requests in ${duration}s - too slow)"
fi

# Test Summary
echo ""
echo "📊 Test Summary"
echo "==============="
echo -e "Total Tests: $((TESTS_PASSED + TESTS_FAILED))"
echo -e "${GREEN}Passed: $TESTS_PASSED${NC}"
echo -e "${RED}Failed: $TESTS_FAILED${NC}"

if [ $TESTS_FAILED -eq 0 ]; then
    echo ""
    echo -e "${GREEN}🎉 All tests passed! AtlasBank is working correctly.${NC}"
    exit 0
else
    echo ""
    echo -e "${RED}❌ Some tests failed. Please check the output above.${NC}"
    exit 1
fi
