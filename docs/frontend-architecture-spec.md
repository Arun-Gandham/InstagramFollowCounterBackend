# Frontend Architecture & UI/UX Specification

Comprehensive technical specification for building the commercial physical Instagram follower counter web application. This document defines the page hierarchy, component architecture, role-based navigation guards, state management, and backend synchronization contracts.

---

## 1. System Overview & Technology Stack

### Recommended Stack
- **Framework:** Angular 19+ (Standalone Components, Signals) OR React 19 (Vite, TypeScript, TailwindCSS, Shadcn UI).
- **HTTP Client:** Native `fetch` or `axios` configured with `withCredentials: true` for automatic HttpOnly session cookie persistence.
- **State Management:**
  - Auth State: Signals / React Context + LocalStorage for non-sensitive user metadata.
  - Server Cache: TanStack Query (React) or RxJS State Services (Angular) with auto-invalidation on mutations.
- **Charts:** Chart.js or Recharts for follower growth analytics.
- **Visual Aesthetic:** Clean, minimalist dark/light mode with a tactile mechanical counter look (split-flap / rotary drum animations) reflecting the physical product hardware.

---

## 2. Authentication, RBAC & Route Guard Architecture

```
                                 [App Router]
                                       |
                   +-------------------+-------------------+
                   |                                       |
            [Public Routes]                        [Protected Routes]
                   |                                       |
          (PublicOnlyGuard)                           (AuthGuard)
                   |                                       |
        +----------+----------+               +------------+------------+
        |                     |               |            |            |
     /login               /register       /dashboard   /devices    /instagram
                                              |
                                              v
                                         (RoleGuard)
                                              |
                             +----------------+----------------+
                             |                                 |
                     [SuperAdmin & Admin]                  [Support]
                             |                                 |
                     /admin/dashboard                  /support/lookup
                     /admin/devices                    /support/devices/:id
                     /admin/users
                     /admin/audit-logs
```

### Route Guard Matrix

| Route | Minimum Role Required | Guard Class | Behavior on Unauthorized |
|---|---|---|---|
| `/login`, `/register`, `/forgot-password` | Public Only | `PublicOnlyGuard` | If already logged in, redirect to `/dashboard` |
| `/verify-email`, `/reset-password` | Public | None | Accessible by any user with token |
| `/dashboard`, `/devices`, `/instagram`, `/settings` | `Customer` | `AuthGuard` | If not logged in, redirect to `/login?returnUrl=...` |
| `/admin/*` | `Admin` or `SuperAdmin` | `RoleGuard(['Admin', 'SuperAdmin'])` | If not admin, show `403 Forbidden` screen |
| `/support/*` | `Support`, `Admin`, `SuperAdmin` | `RoleGuard(['Support', 'Admin', 'SuperAdmin'])` | If unauthorized, show `403 Forbidden` screen |

---

## 3. Detailed Page-by-Page Specifications

### 3.1 Public & Authentication Pages

#### 1. Landing Page (`/`)
- **Purpose:** Commercial storefront showcasing the physical follower counter.
- **Components:**
  - Hero banner with 3D / interactive digital replica of the mechanical follower counter.
  - Features grid: Real-time stepper motor reels, zero-lag polling, silent night mode, drop-resistant CNC aluminum enclosure.
  - "Get Yours Now" order CTA button.
  - Top navigation bar: "Features", "Specs", "FAQ", "Login", "Register".

#### 2. Login Screen (`/login`)
- **Backend API:** `POST /api/v1/auth/login`
- **Fields:** Email (`type="email"`), Password (`type="password"`), "Remember me" checkbox.
- **Actions:**
  - Submits credentials with `withCredentials: true`.
  - On `200 OK`: stores user profile in Auth store, queries `GET /api/v1/auth/me`, redirects to `/dashboard` (or `returnUrl`).
  - On `401 Unauthorized`: renders inline error alert: *"Invalid email or password"*.
  - On `429 Too Many Requests`: displays rate-limit cooldown message.
  - Links to `/register` and `/forgot-password`.

#### 3. Register Screen (`/register`)
- **Backend API:** `POST /api/v1/auth/register`
- **Fields:** Display Name, Email, Password, Password Confirmation.
- **Client Validation:**
  - Password minimum 8 chars, at least 1 uppercase, 1 lowercase, 1 number, 1 special symbol.
  - Real-time password strength meter.
- **On Submit:** Shows success modal instructing the user to check their email for the confirmation link.

#### 4. Email Verification Screen (`/verify-email`)
- **Backend API:** `POST /api/v1/auth/verify-email`
- **URL Parameters:** `?userId=<GUID>&token=<TOKEN>`
- **Behavior:**
  - Automatically submits verification token on page load.
  - Displays animated checkmark icon upon verification success with a *"Proceed to Login"* button.
  - If token expired or invalid, displays an *"Invalid or expired link"* card with a button to resend verification.

#### 5. Password Reset Flow (`/forgot-password` & `/reset-password`)
- **APIs:** `POST /api/v1/auth/forgot-password`, `POST /api/v1/auth/reset-password`
- **Forgot Password:** Submits email and displays neutral privacy-preserving confirmation: *"If an account exists, a reset link has been dispatched."*
- **Reset Password:** Captures `userId`, `token`, and `newPassword`. On success, redirects to `/login` with notification toast.

---

### 3.2 Customer Portal Pages

#### 1. Customer Dashboard (`/dashboard`)
- **Primary Viewport:**
  - **Live Digital Counter Display:** Large interactive split-flap / reel counter widget rendering the customer's live follower count with mechanical rolling sound/animation.
  - **Hardware Counter Status Tile:**
    - Serial Number (e.g. `FC-A82F32`).
    - Connectivity badge: `Online` (green pulse) vs `Offline / Stale` (amber pulse).
    - Last Seen heartbeat: *"Active 12 seconds ago"*.
    - Current Firmware: `v1.0.4`.
  - **Linked Instagram Account Tile:**
    - Profile avatar, `@username`, verified badge, Account Type (`Creator` / `Business`).
    - Connection health status: `Connected` (green) or `Needs Reauthorization` (red banner).
    - "Refresh Now" on-demand button (triggers `POST /api/v1/instagram/accounts/{id}/refresh`).
  - **Quick Setup Alert Banner (if unconfigured):**
    - If customer owns no counter: *"Claim your physical counter to begin"* -> opens Claim modal.
    - If customer has no linked Instagram: *"Connect your Instagram account"* -> triggers OAuth.

#### 2. Devices Management (`/devices`)
- **Backend APIs:**
  - `GET /api/v1/devices` (lists claimed counters)
  - `POST /api/v1/devices/claim` (claims new counter)
  - `POST /api/v1/devices/{id}/instagram/{igId}` (binds counter to Instagram)
- **UI Elements:**
  - Card grid of all physical counters registered to the account.
  - Each card shows: Serial number, binding status, last online time, firmware version, signal strength.
  - "Claim New Counter" floating action button.

#### 3. Claim Counter Wizard Modal (`/devices/claim`)
- **Step 1:** Customer enters **Serial Number** found on the bottom of the device (e.g. `FC-A82F32`).
- **Step 2:** Customer enters **Claim Code** found on the security scratch card inside the packaging (e.g. `CLM-82F3-2ABC-9999`).
- **API Call:** `POST /api/v1/devices/claim`
- **Error Handling:**
  - `409 Conflict`: *"This counter has already been claimed by another account. Please contact support."*
  - `400 Bad Request`: *"Invalid claim code entered. Check the scratch card."*
- **Success State:** Confetti animation, displays paired device confirmation, prompts user to select which Instagram profile should display on it.

#### 4. Instagram Connection Page (`/instagram`)
- **Backend APIs:**
  - `GET /api/v1/instagram/connect` (initiates OAuth)
  - `GET /api/v1/instagram/callback` (handles redirect)
  - `GET /api/v1/instagram/accounts` (lists connected accounts)
  - `DELETE /api/v1/instagram/accounts/{id}` (disconnects account)
- **UI Elements:**
  - Connected account cards showing: `@username`, followers, last refresh time.
  - "Connect Instagram" button: calls `GET /api/v1/instagram/connect`, receives Meta authorization URL, and opens Meta's login page in the browser.
  - Re-authorization warnings: If Meta access token expires or user changes password, displays alert: *"Meta connection expired. Click here to reconnect."*

#### 5. Follower Growth Analytics (`/analytics`)
- **Backend API:** Queries historical follower transitions from backend audit/history.
- **UI Elements:**
  - Line graph showing hourly/daily follower counts.
  - Net change cards: *"Today: +42"*, *"This Week: +310"*, *"This Month: +1,420"*.

#### 6. Account & Security Settings (`/settings`)
- **Backend APIs:**
  - `GET /api/v1/auth/me`
  - `POST /api/v1/auth/change-password`
  - `POST /api/v1/auth/delete-account`
- **UI Elements:**
  - Display name edit form.
  - Change password form (Current password, New password).
  - Logout all sessions button.

---

### 3.3 SuperAdmin & Admin Portal (`/admin/*`)

#### 1. Admin Dashboard (`/admin/dashboard`)
- **Key Metrics Tiles:**
  - Total Counters Manufactured & Provisioned.
  - Total Counters Claimed by Customers.
  - Total Active Instagram Profiles Tracked.
  - Meta API Health & Quota Consumption.

#### 2. Factory Provisioning Tool (`/admin/devices`)
- **Backend API:** `POST /api/v1/admin/devices`, `GET /api/v1/admin/devices`
- **Use Case:** Used at the manufacturing / assembly line before shipping counters in boxes.
- **Workflow:**
  1. Operator clicks "Provision Device".
  2. Submits Serial Number (or auto-generates sequential serial: `FC-XXXXXX`).
  3. Backend generates:
     - 256-bit `plaintextDeviceSecret` (flashed onto ESP32 firmware).
     - Single-use `plaintextClaimCode` (printed on the packaging card).
  4. UI renders printable thermal label with:
     - Serial number text and barcode.
     - Device WiFi setup QR code.
     - Scratch-off packaging claim code.

#### 3. User Management (`/admin/users`)
- **Backend API:** `GET /api/v1/admin/users`
- **Features:** Search by email or display name, filter by status (`Active`, `Suspended`), view owned devices count, view linked Instagram accounts count.

#### 4. Audit Log Explorer (`/admin/audit-logs`)
- **Backend API:** `GET /api/v1/admin/audit-logs`
- **Features:** Searchable table of all security and administrative events with timestamp, IP address hash, action name, user ID, device serial, and JSON metadata viewer.

#### 5. System Health & Background Workers (`/admin/system-health`)
- **Backend API:** `GET /api/v1/admin/system-health`, `GET /health/ready`
- **Features:**
  - PostgreSQL database latency and active connection pool.
  - Follower polling background worker lease status.
  - Token refresh worker queue backlog.

---

### 3.4 Support Portal (`/support/*`)

#### 1. Customer & Counter Lookup (`/support/lookup`)
- Look up counters by Serial Number (`FC-XXXXXX`) or customer email.
- Displays device status, last heartbeat timestamp, firmware version, and binding status.

#### 2. Device Diagnostic Inspector (`/support/devices/:serial`)
- View ESP32 hardware diagnostics:
  - Last seen heartbeat timestamp.
  - WiFi signal strength (RSSI in dBm).
  - ESP32 free heap memory in bytes.
  - Device uptime in seconds.
  - Unclaim / reset counter button (requires administrative approval).

---

## 4. Frontend Component Hierarchy Tree

```
AppComponent
├── NavbarComponent (Brand logo, live sync indicator, user dropdown, theme switcher)
├── RouterOutlet
│   ├── [Public]
│   │   ├── LandingComponent
│   │   ├── LoginComponent
│   │   ├── RegisterComponent
│   │   ├── VerifyEmailComponent
│   │   └── ForgotPasswordComponent
│   │
│   ├── [Customer Portal - SidebarLayoutComponent]
│   │   ├── DashboardComponent
│   │   │   ├── SplitFlapCounterWidget (Animated digits, sound effects)
│   │   │   ├── DeviceStatusTileComponent
│   │   │   └── InstagramProfileTileComponent
│   │   ├── DevicesComponent
│   │   │   ├── DeviceCardComponent
│   │   │   └── ClaimDeviceModalComponent (Step 1: Serial, Step 2: Code)
│   │   ├── InstagramComponent
│   │   │   ├── InstagramAccountCardComponent
│   │   │   └── BindDeviceModalComponent
│   │   ├── AnalyticsComponent (Follower growth charts)
│   │   └── SettingsComponent
│   │
│   └── [Admin Portal - AdminLayoutComponent]
│       ├── AdminDashboardComponent
│       ├── DeviceProvisioningComponent (Factory label printer)
│       ├── UserManagementComponent
│       ├── AuditLogViewerComponent
│       └── SystemHealthComponent
└── FooterComponent
```
