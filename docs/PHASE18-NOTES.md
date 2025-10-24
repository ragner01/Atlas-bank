# Phase 18 — Public Trust Portal + Badges + Regulator API + Open Data

## What you got
- **Public Portal** (`/portal`):
  - Search trust score for any entity (merchant/account/device).
  - Auto-generates an **embeddable SVG badge** (`/badge/{entityId}.svg`).
  - Lists **Open-Data** weekly snapshots (CSV + JSON).
  - Shows latest **Transparency Digest** (immutable audit tip) from Phase 17.
- **Regulator API** (`/regulator/v1/...`):
  - API-key gated (`X-API-Key`) with **HMAC signature** over response payload.
  - Returns Trust score + transparency digest in a single verifiable envelope.
- **Open-Data Exporter** (weekly worker):
  - Aggregates 7-day merchant stats, publishes CSV & JSON to Blob, and updates `index.json`.
  - Designed for external research/oversight and press kits.

## Why this leapfrogs the market
- Nigerian fintechs rarely offer **public reputation** + **verifiable integrity** + **open data**.
- You're shipping **trust as a feature**: badges merchants can wear, datasets regulators can audit, and a digest anyone can verify.

## Security/ops notes
- Put `trust-portal` on a public subnet; keep `trust` core internal.
- Rotate `REGULATOR_API_KEY` & `REGULATOR_API_SIG`.
- Optionally front with WAF + CDN; cache badge SVGs for 60–300s.

## Architecture Components

### Trust Portal (`trust-portal`)
- **Public-facing web interface** for trust score lookup
- **SVG badge generation** for merchant websites
- **Open data browser** for transparency
- **Rate limiting** (200 requests/second)
- **Security**: No hardcoded secrets, proper validation

### Trust Export Worker (`trust-export`)
- **Weekly data export** to Azure Blob Storage
- **CSV and JSON formats** for different use cases
- **Merchant aggregation** with trust band classification
- **Index generation** for easy discovery

### Regulator API
- **API key authentication** (`X-API-Key` header)
- **HMAC signature** for response integrity
- **Combined trust + transparency** data
- **Audit trail** for regulatory compliance

## Trust Score Bands
- **EXCELLENT** (≥80%): High-volume merchants (>₦10M weekly)
- **GOOD** (≥60%): Medium-volume merchants (>₦2M weekly)
- **FAIR** (≥40%): Low-volume merchants (>₦200K weekly)
- **RISKY** (<40%): New or low-volume merchants

## Open Data Format
```json
{
  "merchant": "merchant::m-123",
  "credited_minor": 15000000,
  "tx_count": 45,
  "band": "GOOD"
}
```

## Badge Integration
```html
<img alt="Atlas Trust" src="https://trust.atlasbank.com/badge/m-123.svg" />
```

## Regulator API Response
```json
{
  "data": {
    "entityId": "m-123",
    "trust": {
      "entityId": "m-123",
      "score": 0.75,
      "band": "GOOD"
    },
    "digest": {
      "seq": 12345,
      "root": "abc123..."
    }
  },
  "signature": "def456..."
}
```

## Environment Variables
- `TRUST_CORE`: Trust Core service URL
- `REGULATOR_API_KEY`: API key for regulator access
- `REGULATOR_API_SIG`: HMAC signing secret
- `BLOB_CONN`: Azure Blob Storage connection string
- `OPEN_DATA_CONTAINER`: Blob container name
- `LEDGER_CONN`: PostgreSQL connection string
- `EXPORT_EVERY_MIN`: Export frequency in minutes

## Security Features
- **No hardcoded secrets** - All configuration via environment variables
- **Rate limiting** - Prevents abuse of public endpoints
- **HMAC signatures** - Ensures data integrity for regulators
- **Non-root containers** - Security-hardened Docker images
- **Input validation** - Proper sanitization of user inputs
- **CORS protection** - Restricted cross-origin access

## Business Impact
- **Merchant trust badges** - Build customer confidence
- **Regulatory transparency** - Meet compliance requirements
- **Open data initiative** - Demonstrate commitment to transparency
- **Public reputation** - Showcase platform reliability
- **Research enablement** - Support academic and industry research

