-- KYC Applications and Facts Schema
-- AtlasBank KYC/AML Database Schema

-- KYC Applications table
CREATE TABLE IF NOT EXISTS kyc_applications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id VARCHAR(255) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    decided_at TIMESTAMP WITH TIME ZONE,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT now(),
    
    -- Indexes for performance
    CONSTRAINT kyc_applications_status_check CHECK (status IN ('PENDING', 'APPROVED', 'REVIEW', 'REJECT'))
);

-- KYC Facts table (stores verification results)
CREATE TABLE IF NOT EXISTS kyc_facts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    application_id UUID NOT NULL REFERENCES kyc_applications(id) ON DELETE CASCADE,
    key VARCHAR(100) NOT NULL,
    val JSONB NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    
    -- Unique constraint to prevent duplicate facts per application
    UNIQUE(application_id, key)
);

-- AML Cases table
CREATE TABLE IF NOT EXISTS aml_cases (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id VARCHAR(255) NOT NULL,
    case_type VARCHAR(100) NOT NULL,
    priority VARCHAR(50) NOT NULL DEFAULT 'MEDIUM',
    status VARCHAR(50) NOT NULL DEFAULT 'OPEN',
    description TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    created_by VARCHAR(255) NOT NULL DEFAULT 'system',
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(255),
    
    -- Constraints
    CONSTRAINT aml_cases_status_check CHECK (status IN ('OPEN', 'INVESTIGATING', 'ESCALATED', 'RESOLVED', 'CLOSED')),
    CONSTRAINT aml_cases_priority_check CHECK (priority IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')),
    CONSTRAINT aml_cases_type_check CHECK (case_type IN ('SANCTIONS', 'HIGH_VALUE', 'VELOCITY', 'GEO_RISK', 'TIME_RISK', 'MANUAL'))
);

-- AML Case Notes table
CREATE TABLE IF NOT EXISTS aml_case_notes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    case_id UUID NOT NULL REFERENCES aml_cases(id) ON DELETE CASCADE,
    note TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    created_by VARCHAR(255) NOT NULL DEFAULT 'system'
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_kyc_applications_customer_id ON kyc_applications(customer_id);
CREATE INDEX IF NOT EXISTS idx_kyc_applications_status ON kyc_applications(status);
CREATE INDEX IF NOT EXISTS idx_kyc_applications_created_at ON kyc_applications(created_at);

CREATE INDEX IF NOT EXISTS idx_kyc_facts_application_id ON kyc_facts(application_id);
CREATE INDEX IF NOT EXISTS idx_kyc_facts_key ON kyc_facts(key);

CREATE INDEX IF NOT EXISTS idx_aml_cases_customer_id ON aml_cases(customer_id);
CREATE INDEX IF NOT EXISTS idx_aml_cases_status ON aml_cases(status);
CREATE INDEX IF NOT EXISTS idx_aml_cases_priority ON aml_cases(priority);
CREATE INDEX IF NOT EXISTS idx_aml_cases_case_type ON aml_cases(case_type);
CREATE INDEX IF NOT EXISTS idx_aml_cases_created_at ON aml_cases(created_at);

CREATE INDEX IF NOT EXISTS idx_aml_case_notes_case_id ON aml_case_notes(case_id);
CREATE INDEX IF NOT EXISTS idx_aml_case_notes_created_at ON aml_case_notes(created_at);

-- Row Level Security (RLS) for multi-tenancy
-- Note: In production, implement proper tenant isolation
-- ALTER TABLE kyc_applications ENABLE ROW LEVEL SECURITY;
-- ALTER TABLE kyc_facts ENABLE ROW LEVEL SECURITY;
-- ALTER TABLE aml_cases ENABLE ROW LEVEL SECURITY;
-- ALTER TABLE aml_case_notes ENABLE ROW LEVEL SECURITY;

-- Sample data for testing
INSERT INTO kyc_applications (id, customer_id, status, created_at) VALUES 
    ('550e8400-e29b-41d4-a716-446655440000', 'cust_001', 'PENDING', now() - interval '1 hour'),
    ('550e8400-e29b-41d4-a716-446655440001', 'cust_002', 'APPROVED', now() - interval '2 hours'),
    ('550e8400-e29b-41d4-a716-446655440002', 'cust_003', 'REVIEW', now() - interval '30 minutes')
ON CONFLICT (id) DO NOTHING;

INSERT INTO kyc_facts (application_id, key, val) VALUES 
    ('550e8400-e29b-41d4-a716-446655440000', 'bvn', '{"bvn": "12345678901", "ok": true, "verified_at": "2024-01-15T10:00:00Z", "provider": "mock"}'),
    ('550e8400-e29b-41d4-a716-446655440000', 'nin', '{"nin": "12345678901", "ok": true, "verified_at": "2024-01-15T10:05:00Z", "provider": "mock"}'),
    ('550e8400-e29b-41d4-a716-446655440001', 'bvn', '{"bvn": "12345678902", "ok": true, "verified_at": "2024-01-15T08:00:00Z", "provider": "mock"}'),
    ('550e8400-e29b-41d4-a716-446655440001', 'nin', '{"nin": "12345678902", "ok": true, "verified_at": "2024-01-15T08:05:00Z", "provider": "mock"}'),
    ('550e8400-e29b-41d4-a716-446655440001', 'liveness', '{"score": 0.85, "ok": true, "verified_at": "2024-01-15T08:10:00Z", "threshold": 0.6, "provider": "mock"}'),
    ('550e8400-e29b-41d4-a716-446655440001', 'poa', '{"address_hash": "abc123def456ghi789jkl012mno345pqr678stu901vwx234yz", "ok": true, "verified_at": "2024-01-15T08:15:00Z", "provider": "mock"}')
ON CONFLICT (application_id, key) DO NOTHING;

INSERT INTO aml_cases (id, customer_id, case_type, priority, status, description, created_at, created_by) VALUES 
    ('660e8400-e29b-41d4-a716-446655440000', 'cust_001', 'SANCTIONS', 'HIGH', 'OPEN', 'Customer flagged on sanctions list', now() - interval '1 hour', 'system'),
    ('660e8400-e29b-41d4-a716-446655440001', 'cust_002', 'HIGH_VALUE', 'MEDIUM', 'INVESTIGATING', 'High-value transaction detected', now() - interval '2 hours', 'analyst_001'),
    ('660e8400-e29b-41d4-a716-446655440002', 'cust_003', 'VELOCITY', 'LOW', 'RESOLVED', 'Velocity threshold exceeded', now() - interval '1 day', 'system')
ON CONFLICT (id) DO NOTHING;

INSERT INTO aml_case_notes (case_id, note, created_at, created_by) VALUES 
    ('660e8400-e29b-41d4-a716-446655440000', 'Initial review: Customer appears on OFAC sanctions list. Requires manual verification.', now() - interval '1 hour', 'analyst_001'),
    ('660e8400-e29b-41d4-a716-446655440001', 'Transaction amount: 75,000 NGN. Customer history shows similar patterns. Escalating for review.', now() - interval '2 hours', 'analyst_002'),
    ('660e8400-e29b-41d4-a716-446655440001', 'Additional context: Customer is a business account with legitimate high-value transactions.', now() - interval '1 hour', 'analyst_001'),
    ('660e8400-e29b-41d4-a716-446655440002', 'Case resolved: Velocity was due to legitimate business activity. No further action required.', now() - interval '12 hours', 'analyst_003')
ON CONFLICT DO NOTHING;

