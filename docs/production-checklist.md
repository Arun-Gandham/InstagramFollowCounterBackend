# Production Readiness Checklist

**System:** Commercial Physical Instagram Follower Counter Backend  
**Target:** Linux VPS (2 vCPU, 2–4 GB RAM, Docker Compose + Caddy TLS)

---

## 1. Secrets & Credentials Management
- [ ] **Encryption Master Key:** Generate a 256-bit AES key (Base64-encoded 32 bytes) and configure via `SECRET_PROTECTION__KEYS__KEY01` environment variable. Never commit to git.
- [ ] **Active Key Identifier:** Set `SECRET_PROTECTION__ACTIVEKEYID=key01`.
- [ ] **Meta App Credentials:**
  - `META__CLIENTID`: Real Meta App ID.
  - `META__CLIENTSECRET`: Real Meta App Secret.
  - `META__REDIRECTURI`: Real public HTTPS callback URL (`https://api.example.com/api/v1/instagram/callback`).
  - `META__APIVERSION`: Set to current supported version (e.g. `v22.0`).
- [ ] **Database Credentials:** High-entropy PostgreSQL password for `follower_user`.
- [ ] **ASP.NET Core Identity & Data Protection:** Persisted in PostgreSQL database via `DataProtectionKeys` table so sessions survive container restarts.

## 2. Infrastructure & Networking
- [ ] **Public Exposure:** PostgreSQL port `5432` must **NOT** be exposed publicly (bound only to Docker internal network or `127.0.0.1`).
- [ ] **Reverse Proxy (Caddy/Nginx):** Public HTTPS listening on `80/443` with automatic Let's Encrypt TLS certificates.
- [ ] **Firewall (UFW/iptables):** Allow only ports `22` (SSH), `80` (HTTP), `443` (HTTPS).
- [ ] **CORS Configuration:** `CORS__ALLOWEDORIGINS` configured strictly to customer web domain (e.g. `https://counter.example.com`). Wildcard `*` prohibited.

## 3. Database & Migrations
- [ ] **Initial Database Setup:** Run `dotnet ef database update` using the release migration bundle or production startup migration script.
- [ ] **Zero-Downtime Migration Awareness:** Destructive columns/tables are never dropped without a multi-phase deprecation deployment.
- [ ] **Automated Daily Backups:** Configure cron job running `pg_dump` with GPG encryption and off-site replication (see `docs/backup-restore-guide.md`).
- [ ] **Drill Restore Tested:** Verified successful restoration from a backup dump on staging environment.

## 4. Application Security & Hardening
- [ ] **Environment Setting:** Set `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] **Swagger/OpenAPI:** Automatically disabled or protected behind admin authentication in production.
- [ ] **Security Headers Active:**
  - `Strict-Transport-Security: max-age=31536000; includeSubDomains; preload`
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: strict-origin-when-cross-origin`
- [ ] **Rate Limiting Policies Active:** Registration (5/hr), Login (10/min), Claims (5/min), Device Polling (120/min).
- [ ] **Account Lockout:** 5 failed password attempts triggers a 15-minute temporary lockout.
- [ ] **Log Redaction:** Validated that access tokens, client secrets, passwords, device secrets, and claim tokens are never emitted to stdout or log aggregators.

## 5. Meta Integration Readiness
- [ ] **Meta Business Verification:** Completed and approved in Meta Business Manager.
- [ ] **Meta App Review:** `instagram_business_basic` approved.
- [ ] **Meta App Mode:** Switched from "In Development" to "Live".
- [ ] **Privacy Policy & Terms of Service:** Valid public HTTPS URLs configured in Meta App Dashboard.
- [ ] **User Data Deletion Callback:** Endpoint `/api/v1/auth/delete-account` documented and tested.

## 6. Observability & Monitoring
- [ ] **Health Checks:** Monitor `/health/live` (liveness) and `/health/ready` (database & configuration readiness).
- [ ] **Alerting Thresholds:** Set alerts on HTTP 5xx spikes, database connection failures, and token refresh failure rates.
- [ ] **Worker Health:** Ensure background workers are executing without unhandled exceptions.
