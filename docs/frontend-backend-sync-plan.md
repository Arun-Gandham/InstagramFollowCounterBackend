# Frontend & Backend Synchronization Plan & Implementation Roadmap

A phased, production-oriented blueprint for building the physical Instagram follower counter frontend and keeping it in 100% synchronization with the ASP.NET Core backend API.

---

## 1. Phase-Wise Implementation Roadmap

```
+---------------------------------------------------------------------------------------------------+
| PHASE 1: Project Setup, HTTP Client & Auth System                                [COMPLETED]      |
| - Angular 19 Standalone Components, Signal-based State, TailwindCSS styling                       |
| - Auth Store (Login, Register, Email Verification, Session Persistence via HttpOnly Cookies)      |
| - Route Guards (AuthGuard, RoleGuard, PublicOnlyGuard)                                            |
| - Dynamic Multi-tier API ConfigService (localStorage > config.json > environment) + Health Probe  |
+---------------------------------------------------------------------------------------------------+
                                                  |
                                                  v
+---------------------------------------------------------------------------------------------------+
| PHASE 2: Customer Core Workflow (Counter Claiming & Instagram OAuth)             [COMPLETED]      |
| - Claim Device Modal (Serial + Scratch Claim Code validation)                                     |
| - Instagram Connect OAuth Redirect & Callback Handler (Verified with Live Meta API)              |
| - Reconnect Flow (One-click reauthorization without breaking device bindings)                     |
| - Device-to-Instagram Binding Flow & Active Account Switcher                                      |
| - Interactive Digital Split-Flap Animated Follower Counter with Mechanical Sound Effects          |
+---------------------------------------------------------------------------------------------------+
                                                  |
                                                  v
+---------------------------------------------------------------------------------------------------+
| PHASE 3: Administration, Support & Hardware Manufacturing Tools                  [COMPLETED]      |
| - SuperAdmin & Admin Portal (Dashboard, Metrics, Live KPI Cards)                                  |
| - Factory Device Provisioning Tool (Generates Serials, Secrets, Printable Barcode & QR Stickers)  |
| - User Management Directory & RBAC Inspection Table                                               |
| - Audit Logs Explorer with Timestamp, IP Hash & JSON Payload Viewer                               |
| - Hardware Diagnostic Inspector (/device/v1/state state & sequence verification)                  |
+---------------------------------------------------------------------------------------------------+
                                                  |
                                                  v
+---------------------------------------------------------------------------------------------------+
| PHASE 4: Follower Growth Analytics & Real-Time Sync                              [IN PROGRESS]    |
| - Follower Growth Charts (Day / Week / Month)                                                     |
| - On-Demand Manual Refresh with Rate Limit Cooldown Feedback                                      |
| - Mobile Responsive Polish & Dark/Light Mechanical Aesthetic                                     |
| - Firmware Development & Hardware Prototype Integration                                           |
+---------------------------------------------------------------------------------------------------+
```

---

## 2. Phase Details & Backend Endpoint Mapping

### Phase 1: Authentication & RBAC Foundation [COMPLETED]

#### Achievements:
1. Initialized modern **Angular 19** application (`FRONTEND`) with Standalone Components and Signals.
2. Built `ConfigService` featuring a multi-tier runtime URL resolver:
   - Priority 1: User runtime override (`localStorage.getItem('api_url')`).
   - Priority 2: Static runtime file (`public/config.json`).
   - Priority 3: Build-time fallback (`environment.apiUrl`).
3. Built in-app API Endpoint Modal (`ApiConfigModalComponent`) and Navbar status pill (`⚙️ API Connected (7149)` / `⚠️ API Disconnected`) with live `/health/ready` probe.
4. Handled ASP.NET Core HttpOnly cookie sessions with `withCredentials: true` and `SameSite=None; Secure=Always` local dev compatibility.
5. Implemented `AuthGuard`, `RoleGuard`, and `PublicOnlyGuard`.

#### Endpoints Synchronized in Phase 1:
| UI Component / Action | HTTP Method & URL | Request DTO | Response DTO |
|---|---|---|---|
| Login Form | `POST /api/v1/auth/login` | `LoginRequestDto` | `LoginResponseDto` |
| Registration Form | `POST /api/v1/auth/register` | `RegisterRequestDto` | `RegisterResponseDto` |
| Email Verification Link | `POST /api/v1/auth/verify-email` | `VerifyEmailRequestDto` | `{ message: string }` |
| App Initialization / Session Restore | `GET /api/v1/auth/me` | None | `CurrentUserDto` |
| Logout Button | `POST /api/v1/auth/logout` | None | `200 OK` |
| Forgot Password | `POST /api/v1/auth/forgot-password` | `ForgotPasswordRequestDto` | `200 OK` |
| Reset Password | `POST /api/v1/auth/reset-password` | `ResetPasswordRequestDto` | `200 OK` |

---

### Phase 2: Customer Device Claiming & Instagram Connection [COMPLETED]

#### Achievements:
1. Customer device claiming modal validating serial number (`FC-A82F32`) and claim code (`CLM-82F3-2ABC-9999`).
2. Live Meta OAuth integration verified end-to-end:
   - External Meta authorization URL opened cleanly without local path prepending.
   - Handles OAuth callback and securely binds profile `@arun_naturals_official` (158 followers, sequence #2).
3. Added `🔄 Reconnect` action for expired or re-authorization-required Instagram accounts.
4. Built tactile digital Split-Flap follower counter component mimicking physical reel drum mechanics with audio toggle.

#### Endpoints Synchronized in Phase 2:
| UI Component / Action | HTTP Method & URL | Request DTO | Response DTO |
|---|---|---|---|
| My Devices List | `GET /api/v1/devices` | None | `DeviceDto[]` |
| Claim Device Dialog | `POST /api/v1/devices/claim` | `ClaimDeviceRequestDto` | `ClaimDeviceResponseDto` |
| "Connect Instagram" Button | `GET /api/v1/instagram/connect` | None | `{ authorizationUrl: string }` |
| OAuth Redirect Target | `GET /api/v1/instagram/callback` | `?code=...&state=...` | `302 Redirect` to `/dashboard` |
| Connected Accounts List | `GET /api/v1/instagram/accounts` | None | `InstagramAccountDto[]` |
| Bind Device to Account | `POST /api/v1/devices/{devId}/instagram/{igId}` | None | `200 OK` |
| On-Demand Refresh Button | `POST /api/v1/instagram/accounts/{id}/refresh` | None | `RefreshFollowerResultDto` |

---

### Phase 3: Administration, Support & Hardware Tools [COMPLETED]

#### Achievements:
1. SuperAdmin & Admin dashboard with live metric tiles.
2. Factory Provisioning Tool generating manufactured serial numbers, device secrets, and printable thermal labels with barcodes and QR codes.
3. User directory management and full audit log explorer.
4. Support diagnostics inspecting counter state and sequence numbers.

#### Endpoints Synchronized in Phase 3:
| UI Component / Action | HTTP Method & URL | Required Role | Description |
|---|---|---|---|
| Admin KPI Cards | `GET /api/v1/admin/users`, `/admin/devices` | `Admin`, `SuperAdmin` | Platform summary statistics |
| Factory Provisioning Form | `POST /api/v1/admin/devices` | `Admin`, `SuperAdmin` | Generates new counter credentials & claim codes |
| User Directory Table | `GET /api/v1/admin/users` | `Admin`, `SuperAdmin` | Search & inspect customer accounts |
| Audit Trail Explorer | `GET /api/v1/admin/audit-logs` | `Admin`, `SuperAdmin` | Filterable security & administrative logs |
| Counter Diagnostic Tool | `GET /device/v1/state` | `Support`, `Admin` | Inspects counter online status & sequence |
| Platform Health Probe | `GET /health/ready` | `Admin`, `SuperAdmin` | Database and background worker heartbeat |

---

### Phase 4: Follower Growth Analytics & Firmware Hardware Development [IN PROGRESS]

#### Goals:
1. Historical follower growth trend graphs.
2. Production firmware implementation for ESP32 hardware counters.
3. Microcontroller actuator control (stepper reels / split-flaps) and zero-homing sensor calibration.

---

## 3. TypeScript Interfaces Contract Definition

These TypeScript interfaces match the backend C# records with 100% type safety. Save these in `src/app/models/` or `src/types/`:

```typescript
// ============================================================
// 1. Authentication Types
// ============================================================
export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
}

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
  createdAt: string;
  lastLoginAt?: string;
}

// ============================================================
// 2. Physical Device Types
// ============================================================
export type DeviceStatus = 'Manufactured' | 'Unclaimed' | 'Active' | 'Disabled' | 'Revoked';

export interface Device {
  id: string;
  serialNumber: string;
  status: DeviceStatus;
  firmwareVersion?: string;
  lastSeenAt?: string;
  claimedAt?: string;
  createdAt: string;
  linkedInstagramAccount?: {
    id: string;
    username: string;
    followerCount?: number;
    connectionStatus: InstagramConnectionStatus;
  };
}

export interface ClaimDeviceRequest {
  serialNumber: string;
  claimCode: string;
}

export interface ClaimDeviceResponse {
  deviceId: string;
  serialNumber: string;
  claimedAt: string;
}

// ============================================================
// 3. Instagram Types
// ============================================================
export type InstagramConnectionStatus =
  | 'Connected'
  | 'TokenExpiring'
  | 'Refreshing'
  | 'Expired'
  | 'ReauthorizationRequired'
  | 'RateLimited'
  | 'TemporarilyUnavailable'
  | 'Disconnected'
  | 'Error';

export interface InstagramAccount {
  id: string;
  instagramUserId: string;
  username: string;
  accountType?: string;
  connectionStatus: InstagramConnectionStatus;
  followerCount?: number;
  followerSequence: number;
  lastFollowerRefreshAt?: string;
  requiresReauthorization: boolean;
  createdAt: string;
}

export interface InstagramConnectResponse {
  authorizationUrl: string;
}

export interface RefreshFollowerResult {
  accountId: string;
  username: string;
  followerCount: number;
  changed: boolean;
  previousCount: number;
  sequence: number;
  refreshedAt: string;
}

// ============================================================
// 4. Admin Types
// ============================================================
export interface CreateDeviceRequest {
  serialNumber: string;
}

export interface CreateDeviceResponse {
  deviceId: string;
  serialNumber: string;
  plaintextDeviceSecret: string;
  plaintextClaimCode: string;
  claimExpiresAt: string;
}

export interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  emailConfirmed: boolean;
  status: string;
  createdAt: string;
  lastLoginAt?: string;
  deviceCount: number;
  instagramAccountCount: number;
}

export interface AuditLog {
  id: string;
  action: string;
  result: string;
  userId?: string;
  deviceId?: string;
  ipAddressHash?: string;
  timestamp: string;
  correlationId?: string;
  metadataJson?: string;
}
```

---

## 4. Frontend HTTP Client Setup Example

### Angular Configuration (`app.config.ts`):
```typescript
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(
      withFetch(),
      withInterceptors([
        (req, next) => {
          // Clone request with credentials to automatically send/receive HttpOnly auth cookies
          const cloned = req.clone({
            withCredentials: true,
            setHeaders: {
              'Accept': 'application/json'
            }
          });
          return next(cloned);
        }
      ])
    )
  ]
};
```

### React / Axios Configuration (`src/api/client.ts`):
```typescript
import axios from 'axios';

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'https://localhost:7149',
  withCredentials: true, // Automatically includes HttpOnly cookies
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json'
  }
});

// Interceptor for handling global errors & session expiry
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 && !window.location.pathname.startsWith('/login')) {
      // Session expired -> redirect to login
      window.location.href = `/login?returnUrl=${encodeURIComponent(window.location.pathname)}`;
    }
    return Promise.reject(error);
  }
);
```

---

## 5. Seeded Credentials for Testing Sync

When testing the frontend against the running backend, use these pre-seeded accounts:

| Persona | Email | Password | Role / Access Level |
|---|---|---|---|
| **Super Admin** | `arunsaigandham1998@gmail.com` | `G_arunsai@1998` | Full Admin + Customer access |
| **Admin** | `admin@counter.local` | `AdminPass123!` | Factory Provisioning & Logs |
| **Support** | `support@counter.local` | `SupportPass123!` | Device lookup & diagnostics |
| **Customer** | `customer@counter.local` | `CustomerPass123!` | Dashboard, Claiming & Instagram |

### Test Hardware Device for Claiming:
- **Serial Number**: `FC-A82F32`
- **Claim Code**: `CLM-82F3-2ABC-9999`
