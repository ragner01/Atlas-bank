-- AtlasBank Database Optimization Script
-- Phase 12: Enhanced Indexes and Performance Optimizations

-- =============================================
-- LEDGER SERVICE OPTIMIZATIONS
-- =============================================

-- Enhanced indexes for accounts table
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_accounts_tenant_currency 
ON accounts(tenant_id, currency) 
WHERE deleted_at IS NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_accounts_tenant_type 
ON accounts(tenant_id, account_type) 
WHERE deleted_at IS NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_accounts_created_at 
ON accounts(created_at DESC) 
WHERE deleted_at IS NULL;

-- Enhanced indexes for journal_entries table
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_journal_entries_tenant_created 
ON journal_entries(tenant_id, created_at DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_journal_entries_tenant_status 
ON journal_entries(tenant_id, status) 
WHERE status IN ('PENDING', 'POSTED');

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_journal_entries_correlation_id 
ON journal_entries(correlation_id) 
WHERE correlation_id IS NOT NULL;

-- Enhanced indexes for postings table
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_postings_account_tenant 
ON postings(account_id, tenant_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_postings_tenant_created 
ON postings(tenant_id, created_at DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_postings_entry_account 
ON postings(journal_entry_id, account_id);

-- Enhanced indexes for request_keys table (idempotency)
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_request_keys_tenant_created 
ON request_keys(tenant_id, created_at DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_request_keys_key_hash 
ON request_keys USING hash(key);

-- =============================================
-- PAYMENTS SERVICE OPTIMIZATIONS
-- =============================================

-- Enhanced indexes for payments table
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_tenant_status 
ON payments(tenant_id, status) 
WHERE status IN ('PENDING', 'PROCESSING');

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_tenant_created 
ON payments(tenant_id, created_at DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_correlation_id 
ON payments(correlation_id) 
WHERE correlation_id IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_source_account 
ON payments(source_account_id, tenant_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_destination_account 
ON payments(destination_account_id, tenant_id);

-- Enhanced indexes for idempotency table
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_idempotency_tenant_key 
ON idempotency(tenant_id, key);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_idempotency_created_at 
ON idempotency(created_at DESC);

-- =============================================
-- LOANS SERVICE OPTIMIZATIONS
-- =============================================

-- Enhanced indexes for loans table
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_loans_tenant_status 
ON loans(tenant_id, status) 
WHERE status IN ('ACTIVE', 'PENDING_APPROVAL');

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_loans_tenant_created 
ON loans(tenant_id, created_at DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_loans_customer_id 
ON loans(customer_id, tenant_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_loans_product_id 
ON loans(loan_product_id, tenant_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_loans_next_payment_date 
ON loans(next_payment_date) 
WHERE status = 'ACTIVE' AND next_payment_date IS NOT NULL;

-- Enhanced indexes for installments table
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_installments_loan_tenant 
ON installments(loan_id, tenant_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_installments_due_date 
ON installments(due_date) 
WHERE status IN ('PENDING', 'OVERDUE');

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_installments_loan_due_date 
ON installments(loan_id, due_date);

-- Enhanced indexes for repayments table
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_repayments_loan_tenant 
ON repayments(loan_id, tenant_id);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_repayments_tenant_created 
ON repayments(tenant_id, created_at DESC);

-- =============================================
-- AML/KYC SERVICE OPTIMIZATIONS
-- =============================================

-- Enhanced indexes for aml_cases table
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_aml_cases_tenant_status 
ON aml_cases(tenant_id, status) 
WHERE status IN ('OPEN', 'INVESTIGATING');

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_aml_cases_tenant_created 
ON aml_cases(tenant_id, created_at DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_aml_cases_priority 
ON aml_cases(priority) 
WHERE status = 'OPEN';

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_aml_cases_assigned_to 
ON aml_cases(assigned_to) 
WHERE assigned_to IS NOT NULL;

-- =============================================
-- OUTBOX TABLE OPTIMIZATIONS
-- =============================================

-- Enhanced indexes for outbox table
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_outbox_topic_occurred 
ON outbox(topic, occurred_at ASC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_outbox_occurred_at 
ON outbox(occurred_at ASC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_outbox_key 
ON outbox USING hash(key);

-- =============================================
-- PARTIAL INDEXES FOR PERFORMANCE
-- =============================================

-- Partial indexes for active records only
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_accounts_active_tenant 
ON accounts(tenant_id) 
WHERE deleted_at IS NULL AND status = 'ACTIVE';

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_loans_active_customer 
ON loans(customer_id, tenant_id) 
WHERE status = 'ACTIVE';

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_recent_tenant 
ON payments(tenant_id, created_at DESC) 
WHERE created_at >= NOW() - INTERVAL '30 days';

-- =============================================
-- COMPOSITE INDEXES FOR COMPLEX QUERIES
-- =============================================

-- Multi-column indexes for common query patterns
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_journal_entries_tenant_status_created 
ON journal_entries(tenant_id, status, created_at DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_postings_tenant_account_created 
ON postings(tenant_id, account_id, created_at DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_tenant_status_created 
ON payments(tenant_id, status, created_at DESC);

-- =============================================
-- STATISTICS AND ANALYZE
-- =============================================

-- Update table statistics for better query planning
ANALYZE accounts;
ANALYZE journal_entries;
ANALYZE postings;
ANALYZE request_keys;
ANALYZE payments;
ANALYZE idempotency;
ANALYZE loans;
ANALYZE installments;
ANALYZE repayments;
ANALYZE aml_cases;
ANALYZE outbox;

-- =============================================
-- VACUUM AND MAINTENANCE
-- =============================================

-- Vacuum tables to reclaim space and update statistics
VACUUM ANALYZE accounts;
VACUUM ANALYZE journal_entries;
VACUUM ANALYZE postings;
VACUUM ANALYZE request_keys;
VACUUM ANALYZE payments;
VACUUM ANALYZE idempotency;
VACUUM ANALYZE loans;
VACUUM ANALYZE installments;
VACUUM ANALYZE repayments;
VACUUM ANALYZE aml_cases;
VACUUM ANALYZE outbox;

-- =============================================
-- PERFORMANCE MONITORING VIEWS
-- =============================================

-- Create view for monitoring index usage
CREATE OR REPLACE VIEW index_usage_stats AS
SELECT 
    schemaname,
    tablename,
    indexname,
    idx_scan,
    idx_tup_read,
    idx_tup_fetch
FROM pg_stat_user_indexes
WHERE schemaname = 'public'
ORDER BY idx_scan DESC;

-- Create view for monitoring table sizes
CREATE OR REPLACE VIEW table_sizes AS
SELECT 
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size,
    pg_total_relation_size(schemaname||'.'||tablename) as size_bytes
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;

-- =============================================
-- DATA RETENTION POLICIES
-- =============================================

-- Create function for data retention
CREATE OR REPLACE FUNCTION cleanup_old_data(retention_days INTEGER DEFAULT 365)
RETURNS INTEGER AS $$
DECLARE
    deleted_count INTEGER := 0;
BEGIN
    -- Clean up old journal entries (keep for audit)
    DELETE FROM journal_entries 
    WHERE created_at < NOW() - INTERVAL '1 day' * retention_days
    AND status = 'CANCELLED';
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    
    -- Clean up old request keys (idempotency)
    DELETE FROM request_keys 
    WHERE created_at < NOW() - INTERVAL '1 day' * retention_days;
    
    -- Clean up old outbox messages
    DELETE FROM outbox 
    WHERE occurred_at < NOW() - INTERVAL '1 day' * retention_days;
    
    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

-- =============================================
-- PERFORMANCE TUNING SETTINGS
-- =============================================

-- Optimize PostgreSQL settings for AtlasBank workload
-- Note: These should be set in postgresql.conf or via ALTER SYSTEM

-- Memory settings
-- shared_buffers = 256MB (25% of RAM for small instances)
-- effective_cache_size = 1GB (75% of RAM)
-- work_mem = 4MB
-- maintenance_work_mem = 64MB

-- Connection settings
-- max_connections = 100
-- shared_preload_libraries = 'pg_stat_statements'

-- Checkpoint settings
-- checkpoint_completion_target = 0.9
-- wal_buffers = 16MB
-- checkpoint_timeout = 5min

-- Query planning
-- random_page_cost = 1.1 (for SSD storage)
-- effective_io_concurrency = 200 (for SSD storage)

-- Logging for performance monitoring
-- log_statement = 'mod'
-- log_min_duration_statement = 1000
-- log_checkpoints = on
-- log_connections = on
-- log_disconnections = on
