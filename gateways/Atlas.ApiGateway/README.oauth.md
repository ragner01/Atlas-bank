# Gateway OAuth & Rate Limiting
- Validates JWT from your IdP (Authority/Audience in appsettings).
- Enforces scope-based route policies.
- Applies Redis-backed fixed window rate limit (default 120 req/min).
