-- Cards Vault Schema - PCI DSS Compliant
-- This table stores tokenized card data with proper encryption

CREATE TABLE IF NOT EXISTS cards (
  card_token  text PRIMARY KEY,              -- PAN-less token
  dek_id      text NOT NULL,                 -- id for wrapped DEK (for rotation; stored in ciphertext blob for demo)
  pan_ct      bytea NOT NULL,                -- ciphertext blob (nonce|ct|tag|wrappedDEK)
  aad         bytea NOT NULL,                -- AEAD AAD (expiry)
  bin         text NOT NULL,                 -- Bank Identification Number (first 6 digits)
  last4       text NOT NULL,                 -- Last 4 digits of PAN
  network     text NOT NULL,                 -- Card network (VISA, MASTERCARD, etc.)
  exp_m       text NOT NULL,                 -- Expiry month
  exp_y       text NOT NULL,                 -- Expiry year
  status      text NOT NULL DEFAULT 'Active', -- Card status
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_at  timestamptz NOT NULL DEFAULT now()
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS ix_cards_network ON cards(network);
CREATE INDEX IF NOT EXISTS ix_cards_bin ON cards(bin);
CREATE INDEX IF NOT EXISTS ix_cards_status ON cards(status);
CREATE INDEX IF NOT EXISTS ix_cards_created_at ON cards(created_at);

-- Security constraints
ALTER TABLE cards ADD CONSTRAINT chk_cards_status CHECK (status IN ('Active', 'Suspended', 'Expired', 'Cancelled'));
ALTER TABLE cards ADD CONSTRAINT chk_cards_network CHECK (network IN ('VISA', 'MASTERCARD', 'AMEX', 'DISCOVER', 'CARD'));

-- Restrict UPDATE/DELETE in prod; only status transitions allowed via controlled procedure
-- In production, implement stored procedures for card lifecycle management

-- Audit trail table for card operations
CREATE TABLE IF NOT EXISTS card_audit (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  card_token  text NOT NULL REFERENCES cards(card_token),
  operation   text NOT NULL,                 -- TOKENIZE, AUTHORIZE, SUSPEND, etc.
  amount_minor bigint,                       -- For authorization operations
  currency    text,                          -- For authorization operations
  merchant_id text,                          -- For authorization operations
  auth_code   text,                          -- Authorization code if applicable
  rrn         text,                          -- Retrieval Reference Number if applicable
  status      text NOT NULL,                 -- SUCCESS, FAILED, DECLINED
  error_code  text,                          -- Error code if failed
  created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_card_audit_token ON card_audit(card_token);
CREATE INDEX IF NOT EXISTS ix_card_audit_operation ON card_audit(operation);
CREATE INDEX IF NOT EXISTS ix_card_audit_created_at ON card_audit(created_at);

-- Function to update card status with audit trail
CREATE OR REPLACE FUNCTION update_card_status(
  p_card_token text,
  p_new_status text,
  p_operation text DEFAULT 'STATUS_CHANGE'
) RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
  -- Validate status
  IF p_new_status NOT IN ('Active', 'Suspended', 'Expired', 'Cancelled') THEN
    RAISE EXCEPTION 'Invalid status: %', p_new_status;
  END IF;
  
  -- Update card status
  UPDATE cards 
  SET status = p_new_status, updated_at = now()
  WHERE card_token = p_card_token;
  
  -- Log audit trail
  INSERT INTO card_audit (card_token, operation, status, created_at)
  VALUES (p_card_token, p_operation, 'SUCCESS', now());
  
  RETURN FOUND;
END $$;
