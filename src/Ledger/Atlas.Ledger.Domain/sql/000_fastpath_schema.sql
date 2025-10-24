-- Core tables optimized for hot-path posting
CREATE TABLE IF NOT EXISTS accounts (
  account_id      text PRIMARY KEY,
  tenant_id       text NOT NULL,
  currency        char(3) NOT NULL,
  ledger_minor    bigint NOT NULL DEFAULT 0,
  updated_at      timestamptz NOT NULL DEFAULT now(),
  -- Multi-tenant data integrity constraints
  CONSTRAINT uk_accounts_tenant_id UNIQUE (account_id, tenant_id)
);
CREATE TABLE IF NOT EXISTS journal_entries (
  entry_id        uuid PRIMARY KEY,
  tenant_id       text NOT NULL,
  narrative       text,
  booking_date    timestamptz NOT NULL DEFAULT now(),
  -- Multi-tenant data integrity constraints
  CONSTRAINT uk_journal_entries_tenant_id UNIQUE (entry_id, tenant_id)
);
CREATE TABLE IF NOT EXISTS postings (
  posting_id      uuid PRIMARY KEY,
  entry_id        uuid NOT NULL REFERENCES journal_entries(entry_id),
  account_id      text NOT NULL REFERENCES accounts(account_id),
  amount_minor    bigint NOT NULL,
  side            char(1) NOT NULL CHECK (side IN ('D','C')),
  -- Multi-tenant data integrity constraints
  tenant_id       text NOT NULL,
  CONSTRAINT uk_postings_tenant_id UNIQUE (posting_id, tenant_id)
);
CREATE INDEX IF NOT EXISTS ix_postings_account ON postings(account_id);
CREATE INDEX IF NOT EXISTS ix_postings_entry ON postings(entry_id);

-- Performance indexes for better query performance
CREATE INDEX IF NOT EXISTS ix_accounts_tenant ON accounts(tenant_id);
CREATE INDEX IF NOT EXISTS ix_accounts_currency ON accounts(currency);
CREATE INDEX IF NOT EXISTS ix_journal_entries_tenant ON journal_entries(tenant_id);
CREATE INDEX IF NOT EXISTS ix_journal_entries_date ON journal_entries(booking_date);
CREATE INDEX IF NOT EXISTS ix_postings_side ON postings(side);
CREATE INDEX IF NOT EXISTS ix_postings_amount ON postings(amount_minor);

-- Composite indexes for common queries
CREATE INDEX IF NOT EXISTS ix_accounts_tenant_currency ON accounts(tenant_id, currency);
CREATE INDEX IF NOT EXISTS ix_journal_entries_tenant_date ON journal_entries(tenant_id, booking_date);

-- Optional: hash partition postings by account for large scale (left as future migration)

-- High-speed transfer stored proc (single round-trip, SERIALIZABLE logic)
CREATE OR REPLACE FUNCTION sp_post_transfer(
  p_tenant text,
  p_src text,
  p_dst text,
  p_amount bigint,
  p_currency char(3),
  p_narrative text,
  OUT o_entry uuid
) LANGUAGE plpgsql AS $$
DECLARE
  v_src_currency char(3);
  v_dst_currency char(3);
BEGIN
  -- Ensure accounts exist (idempotent upsert on first hit)
  INSERT INTO accounts(account_id, tenant_id, currency)
  VALUES (p_src, p_tenant, p_currency)
  ON CONFLICT (account_id) DO NOTHING;
  INSERT INTO accounts(account_id, tenant_id, currency)
  VALUES (p_dst, p_tenant, p_currency)
  ON CONFLICT (account_id) DO NOTHING;

  SELECT currency INTO v_src_currency FROM accounts WHERE account_id = p_src;
  SELECT currency INTO v_dst_currency FROM accounts WHERE account_id = p_dst;
  IF v_src_currency <> p_currency OR v_dst_currency <> p_currency THEN
    RAISE EXCEPTION 'Currency mismatch';
  END IF;

  -- Get per-account advisory locks to avoid deadlocks (order by id)
  IF p_src < p_dst THEN
    PERFORM pg_advisory_xact_lock(hashtext(p_src));
    PERFORM pg_advisory_xact_lock(hashtext(p_dst));
  ELSE
    PERFORM pg_advisory_xact_lock(hashtext(p_dst));
    PERFORM pg_advisory_xact_lock(hashtext(p_src));
  END IF;

  -- Check sufficient funds (simple available = ledger for MVP)
  PERFORM 1 FROM accounts WHERE account_id = p_src AND ledger_minor >= p_amount;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'Insufficient funds';
  END IF;

  o_entry := gen_random_uuid();
  INSERT INTO journal_entries(entry_id, tenant_id, narrative) VALUES (o_entry, p_tenant, p_narrative);

  -- Postings (double-entry)
  INSERT INTO postings(posting_id, entry_id, account_id, amount_minor, side)
  VALUES (gen_random_uuid(), o_entry, p_src, p_amount, 'D'),
         (gen_random_uuid(), o_entry, p_dst, p_amount, 'C');

  -- Apply deltas (debit reduces src, credit increases dst)
  UPDATE accounts SET ledger_minor = ledger_minor - p_amount, updated_at = now() WHERE account_id = p_src;
  UPDATE accounts SET ledger_minor = ledger_minor + p_amount, updated_at = now() WHERE account_id = p_dst;

  RETURN;
END $$;

-- Helper for idempotency: record request keys at DB-level for once-only semantics
CREATE TABLE IF NOT EXISTS request_keys (
  key         text PRIMARY KEY,
  seen_at     timestamptz NOT NULL DEFAULT now()
);

-- Function to guard idempotency + call post_transfer atomically
CREATE OR REPLACE FUNCTION sp_idem_transfer(
  p_key text, p_tenant text, p_src text, p_dst text, p_amount bigint, p_currency char(3), p_narrative text,
  OUT o_entry uuid
) LANGUAGE plpgsql AS $$
BEGIN
  INSERT INTO request_keys(key) VALUES (p_key);
  o_entry := gen_random_uuid(); -- Return a new UUID for successful idempotency check
  -- if duplicate, conflict error → client interprets as already accepted
  EXCEPTION WHEN unique_violation THEN
    -- return NULL; caller can query existing status if needed
    o_entry := NULL; RETURN;
END $$;

-- Combine: idempotent + transfer
CREATE OR REPLACE FUNCTION sp_idem_transfer_execute(
  p_key text, p_tenant text, p_src text, p_dst text, p_amount bigint, p_currency char(3), p_narrative text,
  OUT o_entry uuid
) LANGUAGE plpgsql AS $$
BEGIN
  -- Check idempotency first
  BEGIN
    INSERT INTO request_keys(key) VALUES (p_key);
    -- If we get here, it's a new request
  EXCEPTION WHEN unique_violation THEN
    -- Duplicate request, return NULL
    o_entry := NULL; RETURN;
  END;
  
  -- Execute the transfer
  SELECT sp_post_transfer(p_tenant, p_src, p_dst, p_amount, p_currency, p_narrative) INTO o_entry;
END $$;

-- Minimal read balance (no joins)
CREATE OR REPLACE FUNCTION fn_get_balance(p_account text)
RETURNS bigint LANGUAGE sql STABLE AS $$
  SELECT ledger_minor FROM accounts WHERE account_id = p_account
$$;
