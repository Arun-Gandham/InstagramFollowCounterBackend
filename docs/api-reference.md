# API Reference Specification & Data Lineage Guide

This document describes all API endpoints implemented in the commercial physical Instagram follower counter backend, detailing their use cases, authorization requirements, request/response contracts, rate limits, and database table lineage.

---

## 0. Seeded Test Credentials, Roles & Hardware Counter

The database seeder automatically initializes the system with these credentials:

| Role | Email / Login | Password | Purpose |
|---|---|---|---|
| **SuperAdmin** | `arunsaigandham1998@gmail.com` | `G_arunsai@1998` | Full administrative control, all 16 permissions |
| **Admin** | `admin@counter.local` | `AdminPass123!` | Factory device provisioning & audit log inspection |
| **Support** | `support@counter.local` | `SupportPass123!` | Customer service & device diagnostic inspection |
| **Customer** | `customer@counter.local` | `CustomerPass123!` | End-user account for claiming counters |

### Seeded Hardware Counter (Ready to Claim):
- **Serial Number**: `FC-A82F32`
- **Claim Code**: `CLM-82F3-2ABC-9999`
- **Device Hardware Secret**: `dev_device_secret_256bit_secure_token_99`

### Offline Mock vs Live Meta Mode:
- **Offline / Mock Mode** (`"Instagram:Provider": "Fake"`): `GET /api/v1/instagram/connect` returns a mock URL with `code=mock_auth_code_12345`. Connects test profile `@demo_creator` with `18,920` followers.
- **Live Meta Mode** (`"Instagram:Provider": "Meta"`): **100% verified and active** with Meta App ID `1337614034893316`. Redirects to real `https://www.instagram.com/oauth/authorize`. Successfully verified with live account `@arun_naturals_official` (158 followers, sequence #2). Uses an IPv4-enforced `SocketsHttpHandler` to bypass Windows/ISP IPv6 routing packet drops.

---

## 1. Authentication & User Management (`/api/v1/auth/*`)

### 1.1 `POST /api/v1/auth/register`
- **Use Case:** New customer creates an account on the web portal.
- **Auth:** Public.
- **Rate Limit:** 5 requests / hour / IP.
- **Input (JSON):**
  ```json
  {
    "email": "creator@example.com",
    "password": "SecurePassword123!",
    "displayName": "Jane Doe"
  }
  ```
- **Output:** `201 Created` with RFC7807 problem details on error.
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "creator@example.com",
    "displayName": "Jane Doe",
    "message": "Registration successful. Please verify your email before logging in."
  }
  ```
- **Data Lineage:**
  - Writes to: `AspNetUsers` (creates user with `EmailConfirmed = false`, `Status = Active`, Identity password hash).
  - Triggers: `IEmailService.SendEmailVerificationAsync()` sending verification link with secure token.
  - Writes to: `AuditLogs` (`Action = "UserRegistration"`).

---

### 1.2 `POST /api/v1/auth/verify-email`
- **Use Case:** User clicks verification link received via email.
- **Auth:** Public.
- **Input (JSON):**
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "token": "CfDJ8..."
  }
  ```
- **Output:** `200 OK`
  ```json
  {
    "message": "Email confirmed successfully. You can now log in."
  }
  ```
- **Data Lineage:**
  - Reads from: `AspNetUsers`
  - Updates: `AspNetUsers.EmailConfirmed = true`
  - Writes to: `AuditLogs` (`Action = "EmailVerified"`).

---

### 1.3 `POST /api/v1/auth/login`
- **Use Case:** Customer logs in to access their counter portal.
- **Auth:** Public.
- **Rate Limit:** 10 attempts / min / IP. Account lockouts after 5 consecutive failures for 15 minutes.
- **Input (JSON):**
  ```json
  {
    "email": "creator@example.com",
    "password": "SecurePassword123!"
  }
  ```
- **Output:** `200 OK` with `Set-Cookie` (`HttpOnly`, `Secure`, `SameSite=None` in development / `SameSite=Lax` in production).
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "creator@example.com",
    "displayName": "Jane Doe",
    "roles": ["Customer"]
  }
  ```
- **Development vs Production Cookie Policy:**
  - In local development, the frontend runs on `http://localhost:4200` while the backend runs on `https://localhost:7149`. Modern browsers reject cross-origin cookies if `SameSite=Lax`. Thus, development mode configures `SameSite=None; Secure=Always` so sessions persist smoothly across origins with `withCredentials: true`.
  - In production, Caddy reverse-proxy unifies both frontend and backend under the same origin domain, allowing strict `SameSite=Lax` or `SameSite=Strict`.
- **Data Lineage:**
  - Reads from: `AspNetUsers` (checks `EmailConfirmed`, evaluates password hash, evaluates lockout).
  - Updates: `AspNetUsers.LastLoginAt`, resets `AccessFailedCount` on success or increments on failure.
  - Writes to: `DataProtectionKeys` (ASP.NET cookie ticket encryption).
  - Writes to: `AuditLogs` (`Action = "UserLogin"`, `Result = "Success"` or `"Failure"`).

---

### 1.4 `POST /api/v1/auth/logout`
- **Use Case:** Customer logs out of their browser session.
- **Auth:** Authenticated (`Cookie`).
- **Input:** None.
- **Output:** `200 OK` (clears cookie).
- **Data Lineage:**
  - Writes to: `AuditLogs` (`Action = "UserLogout"`).

---

### 1.5 `POST /api/v1/auth/forgot-password`
- **Use Case:** Customer requests password reset link.
- **Auth:** Public.
- **Rate Limit:** 3 requests / hour / IP.
- **Input (JSON):**
  ```json
  {
    "email": "creator@example.com"
  }
  ```
- **Output:** `200 OK` (Constant message to prevent email enumeration).
  ```json
  {
    "message": "If an account exists for this email, password reset instructions have been sent."
  }
  ```
- **Data Lineage:**
  - Reads from: `AspNetUsers`.
  - Triggers: `IEmailService.SendPasswordResetAsync()`.

---

### 1.6 `POST /api/v1/auth/reset-password`
- **Use Case:** Customer submits new password with reset token.
- **Auth:** Public.
- **Input (JSON):**
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "token": "CfDJ8...",
    "newPassword": "NewSecurePassword123!"
  }
  ```
- **Output:** `200 OK`
  ```json
  {
    "message": "Password has been successfully reset."
  }
  ```
- **Data Lineage:**
  - Reads & Updates: `AspNetUsers.PasswordHash`, `AspNetUsers.SecurityStamp`.
  - Writes to: `AuditLogs` (`Action = "PasswordReset"`).

---

### 1.7 `GET /api/v1/auth/me`
- **Use Case:** Frontend verifies active session and retrieves user profile details.
- **Auth:** Authenticated (`Cookie`).
- **Output:** `200 OK`
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "creator@example.com",
    "displayName": "Jane Doe",
    "roles": ["Customer"],
    "createdAt": "2026-09-14T10:00:00Z",
    "lastLoginAt": "2026-09-14T10:15:00Z"
  }
  ```
- **Data Lineage:**
  - Reads from: `AspNetUsers`, `AspNetUserRoles`.

---

### 1.8 `POST /api/v1/auth/change-password`
- **Use Case:** Authenticated user updates their password.
- **Auth:** Authenticated (`Cookie`).
- **Input (JSON):**
  ```json
  {
    "currentPassword": "OldPassword123!",
    "newPassword": "NewPassword123!"
  }
  ```
- **Output:** `200 OK`.
- **Data Lineage:**
  - Reads & Updates: `AspNetUsers.PasswordHash`.

---

### 1.9 `DELETE /api/v1/auth/delete-account`
- **Use Case:** GDPR / Meta privacy compliance: user requests complete account and token deletion.
- **Auth:** Authenticated (`Cookie`).
- **Input (JSON):**
  ```json
  {
    "password": "Password123!"
  }
  ```
- **Output:** `200 OK`.
- **Data Lineage:**
  - Deletes/Unbinds: `DeviceInstagramBindings`, `Devices.OwnerUserId = null` (unclaimed).
  - Calls: `IInstagramProvider.RevokeAuthorizationAsync()` for all user's linked Instagram accounts.
  - Deletes: `InstagramAccounts`, `InstagramOAuthSessions`, `AspNetUsers`.
  - Writes to: `AuditLogs` (`Action = "AccountDeleted"`).

---

### 1.10 `GET /api/v1/auth/csrf-token`
- **Use Case:** Single-page frontend requests Anti-CSRF token to include in `X-CSRF-TOKEN` header for state-changing calls.
- **Auth:** Authenticated (`Cookie`).
- **Output:** `200 OK`
  ```json
  {
    "token": "CfDJ8..."
  }
  ```

---

## 2. Instagram OAuth & Connection Management (`/api/v1/instagram/*`)

### 2.1 `GET /api/v1/instagram/connect`
- **Use Case:** Initiates official Meta OAuth connection flow.
- **Auth:** Authenticated (`Cookie`).
- **Output:** `200 OK`
  ```json
  {
    "authorizationUrl": "https://www.instagram.com/oauth/authorize?client_id=...&redirect_uri=...&response_type=code&scope=instagram_business_basic&state=..."
  }
  ```
- **Data Lineage:**
  - Generates: Cryptographic random 32-byte state token.
  - Writes to: `InstagramOAuthSessions` (`UserId`, `StateHash = SHA256(state)`, `ExpiresAt = Now + 10m`).

---

### 2.2 `GET /api/v1/instagram/callback`
- **Use Case:** Meta redirects user browser to backend callback with authorization code and state token.
- **Auth:** Handled via cryptographic state token (resolved to user ID).
- **Query Params:** `code`, `state`.
- **Output:** `302 Redirect` to frontend success URL (e.g. `/instagram/connected?status=success`).
- **Data Lineage:**
  - Reads: `InstagramOAuthSessions` where `StateHash == SHA256(state)` and `UsedAt == null` and `ExpiresAt > Now`.
  - Updates: `InstagramOAuthSessions.UsedAt = Now`.
  - External Call: `POST https://api.instagram.com/oauth/access_token` (exchanges code for short-lived token).
  - External Call: `GET https://graph.instagram.com/access_token` (exchanges for 60-day long-lived token).
  - External Call: `GET https://graph.instagram.com/me` (reads `id,username,name,account_type,followers_count`).
  - Cryptography: `ISecretProtector.Encrypt(longLivedToken)`.
  - Writes to: `InstagramAccounts` (upserts row for `(OwnerUserId, InstagramUserId)` with encrypted token).
  - Writes to: `FollowerHistory` (initial follower count entry).
  - Writes to: `AuditLogs` (`Action = "InstagramConnected"`).

---

### 2.3 `GET /api/v1/instagram/accounts`
- **Use Case:** Lists all Instagram accounts connected by the authenticated user.
- **Auth:** Authenticated (`Cookie`).
- **Output:** `200 OK`
  ```json
  [
    {
      "id": "2338166c-59e5-4e2e-a3ca-f404ad99fe40",
      "instagramUserId": "17841400000000",
      "username": "arun_naturals_official",
      "accountType": "CREATOR",
      "connectionStatus": "Connected",
      "requiresReauthorization": false,
      "followerCount": 158,
      "followerSequence": 2,
      "lastFollowerRefreshAt": "2026-09-14T12:45:00Z",
      "tokenExpiresAt": "2026-11-13T12:00:00Z"
    }
  ]
  ```
- **Reauthorization & Reconnect Behavior:**
  - If a user revokes permissions in the Instagram app, changes their password, or if the 60-day token expires without automatic renewal, `connectionStatus` transitions to `ReauthorizationRequired` and `requiresReauthorization = true`.
  - The frontend customer dashboard immediately displays a `🔄 Reconnect` button on the account card. Clicking Reconnect initiates `GET /api/v1/instagram/connect`, allowing the user to refresh their OAuth consent without needing to delete and recreate their device bindings.
- **Data Lineage:**
  - Reads from: `InstagramAccounts` where `OwnerUserId == CurrentUserId`. Never exposes `TokenEncrypted`.

---

### 2.4 `GET /api/v1/instagram/accounts/{id}`
- **Use Case:** Retrieves a single Instagram account status and follower details.
- **Auth:** Authenticated (`Cookie`, verified user owns account).
- **Output:** `200 OK` (Same schema as single item above).
- **Data Lineage:**
  - Reads from: `InstagramAccounts` where `Id == id` and `OwnerUserId == CurrentUserId`.

---

### 2.5 `POST /api/v1/instagram/accounts/{id}/refresh`
- **Use Case:** User clicks "Refresh Now" on web UI. Subject to rate limiting/cooldown (1 per minute).
- **Auth:** Authenticated (`Cookie`, ownership enforced).
- **Output:** `200 OK`
  ```json
  {
    "followerCount": 158,
    "followerSequence": 2,
    "lastFollowerRefreshAt": "2026-09-14T12:45:30Z"
  }
  ```
- **Data Lineage:**
  - Reads: `InstagramAccounts`.
  - Cryptography: `ISecretProtector.Decrypt(TokenEncrypted)`.
  - External Call: `IInstagramProvider.GetFollowerCountAsync()`.
  - Updates: `InstagramAccounts.FollowerCount`, increments `FollowerSequence` if changed, records `FollowerHistory`.

---

### 2.6 `DELETE /api/v1/instagram/accounts/{id}`
- **Use Case:** Disconnects Instagram account, revokes token at Meta, cleans secrets.
- **Auth:** Authenticated (`Cookie`, ownership enforced).
- **Output:** `200 OK`.
- **Data Lineage:**
  - External Call: `IInstagramProvider.RevokeAuthorizationAsync()`.
  - Deletes/Unbinds: `DeviceInstagramBindings` associated with this account.
  - Updates: `InstagramAccounts.TokenEncrypted = null`, `InstagramAccounts.ConnectionStatus = Disconnected`.
  - Writes to: `AuditLogs` (`Action = "InstagramDisconnected"`).

---

## 3. Customer Device Management (`/api/v1/devices/*`)

### 3.1 `POST /api/v1/devices/claim`
- **Use Case:** Customer unpacks physical device and enters serial number and claim code found in packaging.
- **Auth:** Authenticated (`Cookie`).
- **Rate Limit:** 5 claims / min / user.
- **Input (JSON):**
  ```json
  {
    "serialNumber": "FC-A82F32",
    "claimCode": "CLM-82F3-2ABC-9999"
  }
  ```
- **Output:** `200 OK`
  ```json
  {
    "deviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "serialNumber": "FC-A82F32",
    "claimedAt": "2026-09-14T10:20:00Z"
  }
  ```
- **Data Lineage:**
  - Database Transaction:
    - Reads: `Devices` by `SerialNumber` with concurrency check (ensures `OwnerUserId == null` and `Status == Unclaimed`).
    - Reads: `DeviceClaims` where `DeviceId == device.Id` and `ClaimTokenHash == SHA256(claimCode)` and `UsedAt == null` and `ExpiresAt > Now`.
    - Updates: `DeviceClaims.UsedAt = Now`, `DeviceClaims.UsedByUserId = CurrentUserId`.
    - Updates: `Devices.OwnerUserId = CurrentUserId`, `Devices.Status = Active`, `Devices.ClaimedAt = Now`.
  - Writes to: `AuditLogs` (`Action = "DeviceClaimed"`).

---

### 3.2 `GET /api/v1/devices`
- **Use Case:** Lists all physical devices claimed by the authenticated customer.
- **Auth:** Authenticated (`Cookie`).
- **Output:** `200 OK`
  ```json
  [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "serialNumber": "FC-A82F32",
      "status": "Active",
      "firmwareVersion": "1.0.2",
      "lastSeenAt": "2026-09-14T10:25:00Z",
      "claimedAt": "2026-09-14T10:20:00Z",
      "linkedInstagramAccount": {
        "id": "2338166c-59e5-4e2e-a3ca-f404ad99fe40",
        "username": "arun_naturals_official",
        "followerCount": 158
      }
    }
  ]
  ```
- **Data Lineage:**
  - Reads: `Devices` JOIN `DeviceInstagramBindings` JOIN `InstagramAccounts` where `Devices.OwnerUserId == CurrentUserId`.

---

### 3.3 `POST /api/v1/devices/{deviceId}/instagram/{instagramAccountId}`
- **Use Case:** Binds a claimed physical device to display followers from a specific connected Instagram account.
- **Auth:** Authenticated (`Cookie`, ownership enforced on both device and Instagram account).
- **Output:** `200 OK`.
- **Data Lineage:**
  - Reads & Validates: `Devices.OwnerUserId == CurrentUserId` and `InstagramAccounts.OwnerUserId == CurrentUserId`.
  - Database Transaction:
    - Deactivates previous active bindings: `UPDATE DeviceInstagramBindings SET Active = false WHERE DeviceId = deviceId`.
    - Inserts: `DeviceInstagramBindings (DeviceId, InstagramAccountId, Active = true)`.
  - Writes to: `AuditLogs` (`Action = "DeviceBoundToInstagram"`).

---

### 3.4 `DELETE /api/v1/devices/{deviceId}/instagram`
- **Use Case:** Unbinds Instagram account from physical device.
- **Auth:** Authenticated (`Cookie`, ownership enforced).
- **Output:** `200 OK`.
- **Data Lineage:**
  - Updates: `DeviceInstagramBindings.Active = false`.

---

## 4. Hardware Device Protocol (`/device/v1/*`)

> [!NOTE]
> Physical devices (ESP32) authenticate against `/device/v1/*` endpoints using their provisioned factory credentials.
> Two authentication methods are supported:
> 1. **HTTP Headers (Production Standard)**:
>    `X-Device-Serial: FC-A82F32`  
>    `Authorization: Device dev_device_secret_256bit_secure_token_99`
> 2. **Query Parameters (Testing & Low-Memory Microcontrollers)**:
>    `?serial=FC-A82F32&secret=dev_device_secret_256bit_secure_token_99`
>
> Both methods evaluate credentials with constant-time SHA-256 hash comparison against the database.

### 4.1 `GET /device/v1/state`
- **Use Case:** Microcontroller polls periodically (every 30–60 seconds) to obtain the current follower count and sequence number.
- **Auth:** `DeviceAuthentication` (via headers or query parameters).
- **Rate Limit:** 120 requests / min / device.

#### Request Examples:

**Header-based (Firmware Standard):**
```http
GET /device/v1/state HTTP/1.1
Host: localhost:7149
X-Device-Serial: FC-A82F32
Authorization: Device dev_device_secret_256bit_secure_token_99
Accept: application/json
```

**Query parameter (Instant Browser / Curl Test):**
```bash
curl -k "https://localhost:7149/device/v1/state?serial=FC-A82F32&secret=dev_device_secret_256bit_secure_token_99"
```

#### Output (`200 OK`):
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

#### Response Fields & Hardware Logic:
| Field | Type | Meaning & Hardware Behavior |
|---|---|---|
| `deviceId` | string | The serial number of the responding counter. |
| `configured` | boolean | `true` if claimed by a customer; `false` if unclaimed (firmware should display pairing QR code). |
| `instagramConnected` | boolean | `true` if bound to an active Instagram account; `false` if user hasn't selected an account yet (firmware displays "CONNECT IG"). |
| `username` | string | Instagram handle whose followers are tracked. |
| `followers` | integer | Real-time follower count (e.g. `158`). Target position for mechanical reels. |
| `sequence` | integer | **Mechanical Stepper Protection Guard**: Monotonically increasing number that increments **only** when the follower count changes. Microcontroller saves `lastSequence` in flash/NVS and triggers mechanical stepper rotations **only if `sequence > lastSequence`**. Prevents mechanical motor burnout, gear wear, acoustic noise, and battery drain. |
| `isStale` | boolean | **Outage Retention Guard**: Indicates if the backend experienced a temporary upstream error contacting Meta. The follower count is **never** reset to 0; firmware keeps displaying `158` and can illuminate a subtle amber warning LED. |
| `updatedAt` | ISO8601 | Timestamp when the follower count last changed. |
| `serverTime` | ISO8601 | Current UTC clock on server (used for RTC clock synchronization on ESP32). |
| `pollAfterSeconds` | integer | Recommended polling interval (e.g. `30` seconds). Firmware sleeps/delays for this duration. |

- **Data Lineage:**
  - Authenticates: Constant-time SHA-256 hash check of device secret against `Devices.CredentialHash`.
  - Reads: `Devices` JOIN `DeviceInstagramBindings` (where `Active == true`) JOIN `InstagramAccounts`.
  - Updates: `Devices.LastSeenAt = Now`.
  - Guarantees: Never exposes user passwords, emails, Meta Client Secret, or Instagram access tokens.

---

### 4.2 `POST /device/v1/heartbeat`
- **Use Case:** Physical device reports telemetry (firmware version, WiFi signal, heap memory, mechanical position).
- **Auth:** `DeviceAuthentication` (headers or query parameters).
- **Input (JSON):**
  ```json
  {
    "firmwareVersion": "1.0.2",
    "uptimeSeconds": 54220,
    "wifiRssi": -52,
    "freeHeap": 89120,
    "lastSequence": 2,
    "status": "OK"
  }
  ```
- **Output:** `200 OK`
  ```json
  {
    "acknowledged": true,
    "serverTime": "2026-09-14T12:45:15Z"
  }
  ```
- **Data Lineage:**
  - Updates: `Devices.LastSeenAt`, `Devices.FirmwareVersion`.

---

## 5. Administration Endpoints (`/api/v1/admin/*`)

- `GET /api/v1/admin/users`: Paginated customer list with account statuses and device counts.
- `GET /api/v1/admin/devices`: All manufactured and claimed hardware counters.
- `POST /api/v1/admin/devices`: Provision new manufactured device (generates high-entropy credential and claim code).
- `POST /api/v1/admin/devices/{deviceId}/disable`: Temporarily disables a device.
- `POST /api/v1/admin/devices/{deviceId}/reset-claim`: Re-issues a new claim code for an unclaimed or support-verified device.
- `GET /api/v1/admin/instagram-connections`: Aggregate status of all Instagram links and refresh health.
- `GET /api/v1/admin/system-health`: Diagnostic summary of worker leases, queue latencies, and error rates.

---

## 6. Health Checks (`/health/*`)

- `GET /health/live`: Unauthenticated liveness probe (returns `200 OK` if process is running).
- `GET /health/ready`: Readiness probe verifying PostgreSQL database connectivity and configuration integrity.
