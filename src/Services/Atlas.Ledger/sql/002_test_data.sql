-- Database initialization script for AtlasBank
-- This script creates test accounts with sufficient balances for testing

-- Create test accounts with balances
INSERT INTO accounts (account_id, tenant_id, currency, ledger_minor)
VALUES 
    ('card::2196553631873980', 'tnt_demo', 'USD', 1000000),
    ('merchant::merchant123', 'tnt_demo', 'USD', 0),
    ('customer::cust001', 'tnt_demo', 'USD', 500000),
    ('customer::cust002', 'tnt_demo', 'USD', 750000),
    ('bank::atlas', 'tnt_demo', 'USD', 10000000)
ON CONFLICT (account_id) DO UPDATE SET
    ledger_minor = EXCLUDED.ledger_minor,
    updated_at = NOW();

-- Create a test journal entry to establish the initial balances
INSERT INTO journal_entries (entry_id, tenant_id, narrative, booking_date)
VALUES ('550e8400-e29b-41d4-a716-446655440000', 'tnt_demo', 'Initial account setup', NOW())
ON CONFLICT (entry_id) DO NOTHING;

-- Create postings for the initial balances
INSERT INTO postings (posting_id, entry_id, account_id, amount_minor, side)
VALUES 
    ('550e8400-e29b-41d4-a716-446655440001', '550e8400-e29b-41d4-a716-446655440000', 'card::2196553631873980', 1000000, 'C'),
    ('550e8400-e29b-41d4-a716-446655440002', '550e8400-e29b-41d4-a716-446655440000', 'customer::cust001', 500000, 'C'),
    ('550e8400-e29b-41d4-a716-446655440003', '550e8400-e29b-41d4-a716-446655440000', 'customer::cust002', 750000, 'C'),
    ('550e8400-e29b-41d4-a716-446655440004', '550e8400-e29b-41d4-a716-446655440000', 'bank::atlas', 10000000, 'C'),
    ('550e8400-e29b-41d4-a716-446655440005', '550e8400-e29b-41d4-a716-446655440000', 'bank::atlas', 1250000, 'D')
ON CONFLICT (posting_id) DO NOTHING;

-- Verify the balances
SELECT 
    account_id,
    ledger_minor,
    currency,
    updated_at
FROM accounts 
WHERE tenant_id = 'tnt_demo'
ORDER BY account_id;