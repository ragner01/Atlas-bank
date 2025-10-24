-- Extend ledger db with light transaction log for trust metrics
CREATE TABLE IF NOT EXISTS transactions (
  id uuid PRIMARY KEY,
  actor_id text NOT NULL,
  counterparty_id text NOT NULL,
  amount_minor bigint NOT NULL,
  currency text NOT NULL,
  status text NOT NULL,
  created_at timestamptz DEFAULT now()
);

-- Create index for trust scoring queries
CREATE INDEX IF NOT EXISTS idx_transactions_actor_status ON transactions(actor_id, status);

-- Create audit table for transparency digest
CREATE TABLE IF NOT EXISTS gl_audit (
  seq bigserial PRIMARY KEY,
  hash bytea NOT NULL,
  created_at timestamptz DEFAULT now()
);

-- Insert sample audit records for testing
INSERT INTO gl_audit (hash) VALUES 
  (decode('a1b2c3d4e5f6789012345678901234567890abcdef', 'hex')),
  (decode('b2c3d4e5f6789012345678901234567890abcdefa1', 'hex')),
  (decode('c3d4e5f6789012345678901234567890abcdefa1b2', 'hex'))
ON CONFLICT DO NOTHING;
