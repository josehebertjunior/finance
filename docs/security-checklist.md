# Security Audit & Pentest Checklist

High-priority items to review and apply:

- Secrets
  - Ensure `Jwt__Key`, `Admin__Password`, and other secrets are stored in environment variables or a secret store.
  - Rotate secrets and ensure CI injects them securely.

- Authentication & Authorization
  - Enforce strong password policy and account lockout (configured).
  - Enforce refresh token rotation and revocation (implemented).
  - Verify JWT signing key length and entropy (>=256-bit recommended).
  - Validate token lifetime and consider shorter access tokens.

- Cookies & CSRF
  - Refresh cookie: `HttpOnly`, `Secure`, `SameSite=None`, appropriate `Expires` (implemented).
  - CSRF cookie: double-submit pattern used; ensure header validation on state-changing endpoints (implemented).

- Transport & Headers
  - Enforce HTTPS and HSTS (implemented for non-dev).
  - Add Content Security Policy (CSP) header (implemented, tune for external assets).
  - Add other security headers (X-Frame-Options, X-Content-Type-Options, Referrer-Policy) (implemented).

- Rate limiting & Brute force
  - Global rate limiting present; add per-endpoint login limiter (implemented).
  - Consider per-account login attempt limits and CAPTCHAs for suspicious activity.

- Data protection
  - Use EF migrations safely; ensure production DB backups and migration review.
  - Encrypt sensitive data at rest if needed.

- Logging & Monitoring
  - Use structured logging (Serilog) and central sinks (e.g., AppInsights, Seq).
  - Audit logs for auth events: login, logout, refresh, failed attempts (basic logging implemented).

- Infrastructure & Deployment
  - Use managed secret stores (Key Vault) for production if desired; ensure package compatibility.
  - Use distributed rate limiter (Redis) for multi-instance deployments.

- Tests & CI
  - Add integration tests for auth lifecycle (implemented).
  - Add automated security scans (Snyk, Dependabot, static analysis).

- Optional / Next-level
  - Implement MFA for admin accounts.
  - Implement session management UI and revoke sessions.
  - Add monitoring alerts for abnormal activity.

Use this checklist to prioritize fixes; I can start applying the high-priority changes (CSP tuning, cookie expirations, login limiter, lockout settings) and add automated scans to CI. 
