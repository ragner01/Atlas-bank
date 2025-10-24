# Phase 15 — Cards & Tokenization Boundary (PAN-less)

## Overview
This phase implements PCI DSS compliant card tokenization with proper separation between Card Data Environment (CDE) and non-PCI zones. PANs are never exposed outside the PCI boundary.

## Architecture

### PCI Zone (CDE) Services
- **Cards Vault**: Handles PAN tokenization and authorization
- **Network Simulator**: Simulates card network authorization

### Non-PCI Zone Services  
- **Payments API**: Processes card-not-present transactions using tokens

## What you got

### Cards Vault (PCI CDE)
- **`/vault/tokenize`**: 
  - Validates PAN using Luhn algorithm
  - Creates format-preserving token (FPT) using Argon2
  - Encrypts PAN using AES-GCM with per-card DEK
  - Stores encrypted PAN with wrapped DEK
  - Returns PAN-less token for external use

- **`/vault/authorize`**: 
  - Decrypts PAN **inside** CDE only
  - Calls NetworkSim for authorization
  - Returns only approval status, auth_code, rrn, network, last4
  - **Never returns PAN outside CDE**

### Network Simulator (PCI CDE)
- **`/net/auth`**: Minimal "network" simulation
  - Approves low-risk amounts (< 20,000.00)
  - Validates card expiry
  - Declines restricted MCCs (gambling)
  - Returns realistic auth codes and RRNs

### Payments API (Non-PCI)
- **`/payments/cnp/charge`**: 
  - Processes card-not-present transactions
  - Uses card tokens (never PANs)
  - Calls Vault for authorization
  - Posts ledger entry on approval
  - Returns transaction details with auth references

## Security Features

### Encryption & Key Management
- **Per-card DEK**: Each card has unique Data Encryption Key
- **HSM Integration**: DEKs wrapped by Key Encryption Key from HSM
- **AES-GCM**: Authenticated encryption with additional data
- **Mock HSM**: Demo implementation (replace with Azure Managed HSM in prod)

### Tokenization
- **Format-Preserving Tokens**: Maintains PAN format for compatibility
- **Non-reversible**: Uses Argon2 hash for deterministic but irreversible tokens
- **Deterministic**: Same PAN always produces same token

### PCI Compliance
- **CDE Isolation**: Card services run in isolated network segment
- **PAN Handling**: PANs only exist transiently inside CDE
- **Audit Trail**: All operations logged with correlation IDs
- **Access Control**: Services run as non-root users

## Database Schema

### Cards Table
```sql
CREATE TABLE cards (
  card_token  text PRIMARY KEY,    -- PAN-less token
  dek_id      text NOT NULL,       -- DEK identifier
  pan_ct      bytea NOT NULL,      -- Encrypted PAN blob
  aad         bytea NOT NULL,      -- Additional authenticated data
  bin         text NOT NULL,       -- Bank Identification Number
  last4       text NOT NULL,       -- Last 4 digits
  network     text NOT NULL,       -- Card network
  exp_m       text NOT NULL,       -- Expiry month
  exp_y       text NOT NULL,       -- Expiry year
  status      text NOT NULL,       -- Card status
  created_at  timestamptz NOT NULL DEFAULT now()
);
```

### Audit Trail
```sql
CREATE TABLE card_audit (
  id          uuid PRIMARY KEY,
  card_token  text NOT NULL,
  operation   text NOT NULL,       -- TOKENIZE, AUTHORIZE, etc.
  amount_minor bigint,
  currency    text,
  merchant_id text,
  auth_code   text,
  rrn         text,
  status      text NOT NULL,       -- SUCCESS, FAILED, DECLINED
  created_at  timestamptz NOT NULL DEFAULT now()
);
```

## Environment Variables

### Cards Vault
- `CARDS_DB`: Database connection string
- `KEK_LABEL`: Key Encryption Key label
- `NETWORK_SIM`: Network simulator URL

### Payments API
- `Services__VaultBase`: Cards Vault base URL

## Production Considerations

### Security Hardening
- Replace Mock HSM with Azure Managed HSM
- Implement mTLS between services
- Add Web Application Firewall (WAF)
- Enable network segmentation
- Implement proper key rotation

### Monitoring & Compliance
- Add comprehensive logging
- Implement SIEM integration
- Add PCI DSS compliance monitoring
- Set up alerting for security events

### Scalability
- Add Redis caching for token lookups
- Implement horizontal scaling
- Add load balancing
- Consider database sharding

## Roadmap
- **Network Tokenization**: VTS/MDES integration
- **3-D Secure**: Strong Customer Authentication
- **Recurring Payments**: Credential-on-file management
- **Charge Lifecycle**: Capture, refund, reversal operations
- **Clearing & Settlement**: Batch processing for network settlement
