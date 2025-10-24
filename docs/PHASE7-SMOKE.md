# Phase 7 Smoke
1) `make up` to start all services including Loans API
2) Create a loan product:
   ```bash
   curl -s -X POST http://localhost:5221/loans/products \
     -H "Content-Type: application/json" \
     -d '{"name":"NGN 12m 24%","annualRate":0.24,"termMonths":12,"scale":2,"currency":"NGN"}'
   ```
3) Create a loan (replace PRODUCT_ID from step 2):
   ```bash
   curl -s "http://localhost:5221/loans?productId=PRODUCT_ID&customerId=cust_123&principalMinor=50000000"
   ```
4) View loan schedule:
   ```bash
   curl -s http://localhost:5221/loans/LOAN_ID/schedule
   ```
5) Make a repayment:
   ```bash
   curl -s -X POST "http://localhost:5221/loans/LOAN_ID/repayments?amountMinor=6000000&narration=First%20Repayment"
   ```
6) Check schedule again; status flips to Delinquent if over grace window.
7) Write off a loan:
   ```bash
   curl -s -X POST "http://localhost:5221/loans/LOAN_ID/writeoff?reason=Customer%20default"
   ```

## Loan Management Features
- **Amortization Schedules**: Equal installment calculations with compound interest
- **Repayment Allocation**: Interest-first, then principal allocation
- **Delinquency Tracking**: Automatic status updates based on payment history
- **Multi-tenant Support**: Tenant-aware loan products and loans
- **Write-off Management**: Loan closure and write-off capabilities
