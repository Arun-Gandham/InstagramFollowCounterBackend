# Physical Instagram Follower Counter - Backend Platform

Production-ready backend platform for a commercial physical mechanical Instagram follower counter product. Built on **.NET 9 LTS**, **ASP.NET Core Web API**, **Entity Framework Core**, **PostgreSQL**, and **Docker**.

---

## 1. System Architecture

```
                                  +---------------------------------------+
                                  |           Caddy Reverse Proxy         |
                                  |   (Auto-HTTPS / TLS 1.3 / Security)   |
                                  +-------------------+-------------------+
                                                      |
                             +------------------------+------------------------+
                             |                                                 |
                             v                                                 v
             +-------------------------------+                 +-------------------------------+
             |       Physical Device         |                 |    Web Application Client     |
             |       (ESP32 Counter)         |                 |   (Angular 19 SPA / Browser)  |
             +---------------+---------------+                 +---------------+---------------+
                             |                                                 |
             X-Device-Serial | Authorization: Device <Secret>  SameSite=None   | X-CSRF-TOKEN
             Or Query Params | (?serial=..&secret=..)          Secure Cookies  | (Port 4200 -> 7149)
             Rate Limited    | 120 req/min                     withCredentials |
                             v                                                 v
     +-------------------------------------------------------------------------------------------------+
     |                                 ASP.NET Core Web API (.NET 9)                                   |
     |                                                                                                 |
     |  Controllers:                                                                                   |
     |  - /device/v1/*        Hardware Counter state polling (headers or query params), telemetry      |
     |  - /api/v1/auth/*      Identity registration, verification, login, logout, password reset       |
     |  - /api/v1/instagram/* OAuth 2.0 connection, state verification, manual refresh, disconnect     |
     |  - /api/v1/devices/*   Customer device claiming, Instagram binding management                  |
     |  - /api/v1/admin/*     Factory provisioning, serial/claim code generation, audit logs           |
     |  - /health/*           Liveness (/health/live), readiness (/health/ready) probes               |
     +-----------------------------------------------+-------------------------------------------------+
                             |                       |
                             |                       | AES-256-GCM Encryption / Decryption
                             |                       v
                             |         +-----------------------------------------------+
                             |         |     AES-256-GCM Secret Protector              |
                             |         |  (Tokens encrypted before saving to DB)       |
                             |         +-----------------------------------------------+
                             v
             +-------------------------------+                 +-------------------------------+
             |     Background Workers        |                 |      PostgreSQL Database      |
             |  - Token Refresh (Every 12h)  | <=============> |  - Users, Roles, Claims       |
             |  - Follower Poller (Every 60s)|  Distributed    |  - Devices, DeviceClaims      |
             |  - Lease Lock: FOR UPDATE SKIP|  Row Leasing    |  - InstagramAccounts, Bindings|
             |    LOCKED                     |                 |  - FollowerHistories, Audits  |
             +---------------+---------------+                 +-------------------------------+
                             |
                             | Bearer <LongLivedToken>
                             | Forced IPv4 SocketsHttpHandler (59ms latency)
                             v
             +-------------------------------+
             |   Meta Instagram Graph API    |
             |  (Instagram API with Login)   |
             |  - App ID: 1337614034893316   |
             |  - Scope: instagram_business_ |
             |    basic                      |
             |  - 60-day token rotation      |
             +-------------------------------+
```

---

## 2. Solution Structure

```
FollowerCounter/
├── docker/
│   ├── Caddyfile                   # Production reverse proxy, auto-TLS, CSP, security headers
│   ├── Dockerfile                  # Multi-stage build for API and Worker runtime
│   └── docker-compose.yml          # Production orchestration (PostgreSQL, API, Worker, Caddy)
├── docs/
│   ├── api-reference.md            # Comprehensive OpenAPI / REST endpoint specifications
│   ├── backup-restore-guide.md     # pg_dump automated backup script & recovery procedures
│   ├── meta-app-review.md          # Meta App Review submission & permission approval guide
│   ├── meta-instagram-current-api.md# Meta Graph API specifications & token lifecycle details
│   ├── production-checklist.md     # Pre-launch security, performance & reliability checklist
│   └── security-threat-model.md    # STRIDE threat model, attack trees & mitigation matrix
├── src/
│   ├── FollowerCounter.Domain/     # Core domain entities, business rules, enums, exceptions
│   │   ├── Entities/               # AppUser, Device, DeviceClaim, InstagramAccount, etc.
│   │   ├── Enums/                  # DeviceStatus, InstagramConnectionStatus, UserStatus
│   │   └── Exceptions/             # DomainException, NotFoundException, ConflictException
│   ├── FollowerCounter.Application/# Application DTOs, interfaces, and FluentValidation rules
│   │   ├── Common/Interfaces/      # IDeviceService, IInstagramService, IAppDbContext, etc.
│   │   ├── DTOs/                   # Request/response records for Auth, Device, Instagram, Admin
│   │   └── Validators/             # FluentValidation validators with strict regex / length rules
│   ├── FollowerCounter.Infrastructure/ # Database, Security, Cryptography, Meta API integration
│   │   ├── Development/            # FakeInstagramProvider for offline integration tests
│   │   ├── Meta/                   # MetaInstagramProvider implementing live Graph API calls
│   │   ├── Persistence/            # AppDbContext, Entity Configurations, EF Core Migrations
│   │   ├── Security/               # SecretProtector (AES-256-GCM), CryptoHelper (SHA-256)
│   │   ├── Services/               # AuthService, DeviceService, InstagramService, AdminService
│   │   └── Workers/                # InstagramFollowerRefreshWorker, InstagramTokenRefreshWorker
│   ├── FollowerCounter.Api/        # ASP.NET Core Web API Host
│   │   ├── Controllers/            # Auth, Instagram, DeviceManagement, DeviceApi, Admin, Health
│   │   ├── Middlewares/            # ExceptionHandling, SecurityHeaders, CorrelationId
│   │   └── Program.cs              # DI container, Rate Limiting, Cookie Auth, Swagger, Pipeline
│   └── FollowerCounter.Worker/     # Standalone Background Worker host for distributed polling
└── tests/
    ├── FollowerCounter.UnitTests/  # 23 Unit Tests (AES-256-GCM, Domain rules, Validators)
    └── FollowerCounter.IntegrationTests/ # 8 End-to-End Integration Tests (OAuth, Device flow, Race conditions)
```

---

## 3. Technology Stack & Key Decisions

| Component | Choice | Rationale |
|---|---|---|
| **Platform** | .NET 9 LTS (C# 13) | Top-tier runtime performance, native JSON serializer, minimal memory footprint. |
| **Database** | PostgreSQL 17 | Robust relational integrity, partial unique indexing, row-level leasing (`SKIP LOCKED`). |
| **ORM** | Entity Framework Core 9 | Strongly typed schema migrations, connection pooling, retrying execution strategy. |
| **Token Encryption** | AES-256-GCM | Authenticated symmetric encryption prevents ciphertext tampering without separate HMAC. |
| **Device Auth** | `Authorization: Device <Secret>` | Simple, secure, zero-cookie hardware authentication for microcontrollers (ESP32). |
| **Reverse Proxy** | Caddy 2 | Automatic Let's Encrypt TLS renewal, HTTP/3, minimal configuration overhead. |
| **Rate Limiting** | ASP.NET Core RateLimiter | Built-in fixed window partitions for registration, login, device polling, and OAuth. |

---

## 4. Hardware Counter State Contract (`/device/v1/state`)

The physical counter issues an HTTP GET request every 30-60 seconds.

### Authentication Options:

**Method A: Standard Production Headers (Recommended for ESP32 Firmware)**
```http
GET /device/v1/state HTTP/1.1
Host: localhost:7149
X-Device-Serial: FC-A82F32
Authorization: Device dev_device_secret_256bit_secure_token_99
Accept: application/json
```

**Method B: Query Parameters (Instant Browser Testing & Resource-Constrained Microcontrollers)**
```http
GET https://localhost:7149/device/v1/state?serial=FC-A82F32&secret=dev_device_secret_256bit_secure_token_99
```

### Verified Live JSON Response:

```json
{
  "deviceId": "FC-A82F32",
  "configured": true,
  "instagramConnected": true,
  "username": "arun_naturals_official",
  "followers": 158,
  "sequence": 2,
  "isStale": false,
  "updatedAt": "2026-09-14T12:45:00Z",
  "serverTime": "2026-09-14T12:45:15Z",
  "pollAfterSeconds": 30
}
```

### Hardware Guard Parameters:
- **`sequence` (integer)**: A strictly monotonically increasing sequence number that increments **only** when the actual follower count changes. Microcontroller firmware stores `lastSeenSequence` in flash/NVS and executes mechanical reel rotations **only when `sequence > lastSeenSequence`**. This avoids continuous unnecessary stepper motor activation, preventing motor burnout, mechanical gear wear, acoustic noise, and battery drain.
- **`isStale` (boolean)**: Set to `true` if the backend encounters a temporary upstream issue reaching Meta (e.g. Meta Graph API rate limit or network glitch). Crucially, the backend **never sets followers to 0 or null**. The counter continues displaying its last verified follower count (`158`), but can illuminate a small amber warning LED on the physical chassis.

### Mechanical Step Logic on ESP32:
- If `configured == false`: Display pairing QR code or serial on display.
- If `instagramConnected == false`: Display "CONNECT IG" status message on counter.
- If `sequence > lastSeenSequence`: Calculate difference `delta = followers - currentReels`, rotate split-flap / stepper motors to the new follower count, and persist `lastSeenSequence = sequence` in NVS.
- If `isStale == true`: Illuminate amber status LED without resetting or altering the mechanical display.

---

## 5. Security Architecture

1. **At-Rest Token Encryption**:
   Instagram OAuth long-lived tokens and refresh tokens are encrypted using **AES-256-GCM** before touching PostgreSQL. Master encryption keys are loaded exclusively from environment variables (`Security:TokenEncryptionKeyHex`).
2. **Device Secret Protection**:
   Plaintext device secrets and claim codes are generated via `RandomNumberGenerator`. Secrets are hashed using **SHA-256** prior to storage. Authentication checks use fixed-time comparison (`CryptographicOperations.FixedTimeEquals`) to prevent timing attacks. Supports both `Authorization: Device <secret>` + `X-Device-Serial` headers and `?serial=...&secret=...` query parameters.
3. **Double Claim Prevention**:
   Claim codes are bound by a unique SHA-256 token hash and processed within an EF Core execution strategy transaction with atomic row locking.
4. **Outage Retention**:
   During Meta Graph API rate limits or outages, follower counts are **never** cleared, set to zero, or nullified. The counter maintains its last verified count with `isStale: true`.
5. **Cross-Origin Dev Cookies & Middleware**:
   In local development, cookies use `SameSite=None; Secure=Always` so the Angular 19 frontend (`http://localhost:4200`) authenticates seamlessly against `https://localhost:7149`. Middleware order enforces `UseRouting()` and `UseCors()` before `UseHttpsRedirection()` to preserve CORS preflight responses.
6. **IPv4 Meta Socket Handler**:
   Enforces `AddressFamily.InterNetwork` in `SocketsHttpHandler.ConnectCallback` to eliminate Windows/ISP IPv6 packet loss when connecting to `api.instagram.com` and `graph.instagram.com`.

---

## 6. Local Development Setup

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- [Node.js 20+ & npm](https://nodejs.org/) (for Angular 19 frontend)

### Step 1: Clone and Configure Environment
Copy `.env.example` to your environment or configure `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=follower_counter_db;Username=postgres;Password=YourPassword;"
  },
  "Security": {
    "TokenEncryptionKeyHex": "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"
  },
  "MetaOAuth": {
    "ClientId": "1337614034893316",
    "ClientSecret": "YOUR_META_APP_SECRET",
    "RedirectUri": "https://localhost:7149/api/v1/instagram/callback"
  },
  "ClientApp": {
    "BaseUrl": "http://localhost:4200"
  }
}
```

### Step 2: Apply Database Migrations
```bash
dotnet ef database update --project src/FollowerCounter.Infrastructure --startup-project src/FollowerCounter.Api
```

### Step 3: Run the API
```bash
dotnet run --project src/FollowerCounter.Api
```
- API Base: `https://localhost:7149` (or `http://localhost:5033`)
- Swagger UI: `https://localhost:7149/swagger`
- Health Probe: `https://localhost:7149/health/ready`

### Step 4: Run the Angular Frontend
```bash
cd ../FRONTEND
npm install
npm start
```
- Frontend UI: `http://localhost:4200`

---

## 7. Running Automated Tests

The solution includes comprehensive unit tests and integration tests against real PostgreSQL database instances:

```bash
# Run all tests (Unit Tests + Integration Tests)
dotnet test

# Run unit tests only
dotnet test tests/FollowerCounter.UnitTests

# Run integration tests only
dotnet test tests/FollowerCounter.IntegrationTests
```

### Test Suite Summary:
- `FollowerCounter.UnitTests`: 23 tests validating AES-256-GCM encryption/decryption, SHA-256 hashing, timing safety, entity business rules, and FluentValidation rules.
- `FollowerCounter.IntegrationTests`: 10 tests validating end-to-end authentication, cookie sessions, Meta OAuth flow, replay attack prevention, customer device claiming, double claim rejection, follower incrementing, hardware counter state polling, and Swagger accessibility.
- **Total: 33 Tests Passed, 0 Failed.**

---

## 8. Seeded Accounts, Roles & Default Credentials

The database seeder initializes the system with all 4 core roles, assigns all 16 permissions to every role, and creates default accounts:

| Role | Email / Username | Password | Purpose |
|---|---|---|---|
| **SuperAdmin** | `arunsaigandham1998@gmail.com` | `G_arunsai@1998` | Master system control, admin portal, user management |
| **Admin** | `admin@counter.local` | `AdminPass123!` | Factory device provisioning & audit log inspection |
| **Support** | `support@counter.local` | `SupportPass123!` | Customer service & device diagnostic inspection |
| **Customer** | `customer@counter.local` | `CustomerPass123!` | End-user account for claiming counters |

### Seeded Hardware Counter (Ready to Claim):
- **Serial Number**: `FC-A82F32`
- **Claim Code**: `CLM-82F3-2ABC-9999`
- **Device Hardware Secret**: `dev_device_secret_256bit_secure_token_99`

---

## 8. Production Deployment (Docker + Caddy)

### Step 1: Prepare `.env` File
Create `.env` in your production directory:

```env
DOMAIN=counter.yourdomain.com
ACME_EMAIL=admin@yourdomain.com
POSTGRES_DB=follower_counter_prod
POSTGRES_USER=counter_app
POSTGRES_PASSWORD=SuperStrongPostgresPassword987!
TOKEN_ENCRYPTION_KEY_HEX=YOUR_64_CHAR_HEX_AES_256_KEY
META_APP_ID=YOUR_META_APP_ID
META_APP_SECRET=YOUR_META_APP_SECRET
META_REDIRECT_URI=https://counter.yourdomain.com/api/v1/instagram/callback
ADMIN_SEED_EMAIL=admin@yourdomain.com
ADMIN_SEED_PASSWORD=SuperSecureAdminPassword123!
```

### Step 2: Launch with Docker Compose
```bash
cd docker
docker compose up -d --build
```

### Step 3: Verify Deployment
- Health check: `curl -i https://counter.yourdomain.com/health`
- Database readiness probe: `curl -i https://counter.yourdomain.com/health/ready`

---

## 9. Documentation Index

- [Meta Instagram Graph API 2026 Specification](docs/meta-instagram-current-api.md)
- [Meta App Review & Permissions Guide](docs/meta-app-review.md)
- [Security Threat Model & STRIDE Matrix](docs/security-threat-model.md)
- [Production Reliability Checklist](docs/production-checklist.md)
- [API Reference & OpenAPI Specification](docs/api-reference.md)
- [Automated Backup & Disaster Recovery Guide](docs/backup-restore-guide.md)
- [Frontend Architecture & UI/UX Specification](docs/frontend-architecture-spec.md)
- [Frontend & Backend Synchronization Plan](docs/frontend-backend-sync-plan.md)
- [Firmware Architecture & Hardware Selection Guide](docs/firmware-hardware-selection.md)
