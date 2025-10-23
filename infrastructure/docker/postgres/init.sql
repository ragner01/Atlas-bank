-- AtlasBank Database Initialization Script

-- Create databases for each service
CREATE DATABASE atlas_ledger_dev;
CREATE DATABASE atlas_payments_dev;
CREATE DATABASE atlas_cards_dev;
CREATE DATABASE atlas_kyc_dev;
CREATE DATABASE atlas_risk_dev;
CREATE DATABASE atlas_loans_dev;
CREATE DATABASE atlas_fx_dev;
CREATE DATABASE atlas_reporting_dev;
CREATE DATABASE atlas_identity_dev;

-- Grant permissions to atlas user
GRANT ALL PRIVILEGES ON DATABASE atlas_ledger_dev TO atlas;
GRANT ALL PRIVILEGES ON DATABASE atlas_payments_dev TO atlas;
GRANT ALL PRIVILEGES ON DATABASE atlas_cards_dev TO atlas;
GRANT ALL PRIVILEGES ON DATABASE atlas_kyc_dev TO atlas;
GRANT ALL PRIVILEGES ON DATABASE atlas_risk_dev TO atlas;
GRANT ALL PRIVILEGES ON DATABASE atlas_loans_dev TO atlas;
GRANT ALL PRIVILEGES ON DATABASE atlas_fx_dev TO atlas;
GRANT ALL PRIVILEGES ON DATABASE atlas_reporting_dev TO atlas;
GRANT ALL PRIVILEGES ON DATABASE atlas_identity_dev TO atlas;

-- Create extensions for each database
\c atlas_ledger_dev;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

\c atlas_payments_dev;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

\c atlas_cards_dev;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

\c atlas_kyc_dev;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

\c atlas_risk_dev;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

\c atlas_loans_dev;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

\c atlas_fx_dev;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

\c atlas_reporting_dev;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

\c atlas_identity_dev;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
