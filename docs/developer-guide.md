# Developer Guide

High level notes:

- Backend: ASP.NET Core 8, Identity, JWT access tokens + HttpOnly refresh cookie. DB: SQLite for dev.
- Frontend: Angular 21 standalone components. Access token is kept in memory; refresh token stored as HttpOnly cookie.

Key files:

- `FinanceApp.Api/Endpoints/AuthEndpoints.cs` — auth endpoints.
- `FinanceApp.Api/Models/RefreshToken.cs` — refresh token metadata.
- `finance-app-ui/src/app/services/auth.service.ts` — client auth service.

Next steps for production:

- Move JWT key and admin credentials to Key Vault / env vars.
- Harden refresh token rotation and revoke logic; implement device tracking.
- Add monitoring and rate limiting (e.g., App Insights, Prometheus, Redis rate limiter).
