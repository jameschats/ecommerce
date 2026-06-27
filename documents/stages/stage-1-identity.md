# Stage 1 — Identity (Authentication & RBAC)

**Goal:** multi-provider sign-in (admin-toggleable) + role/permission authorization, all converging on one JWT. See [`../design.md`](../design.md) §12.

## Scope & checklist

### Backend (✅ done)
- [x] Hand-authored entities: User, Role, Permission, UserRole, RolePermission, AuthProvider, UserExternalLogin, OtpVerification, RefreshToken
- [x] BCrypt password hashing (`IPasswordHasher`)
- [x] JWT access tokens + SHA-256-hashed rotating refresh tokens (`IJwtTokenService`)
- [x] **Email + Password**: register / login (+ `AllowRegistration` toggle)
- [x] **Mobile OTP** (passwordless): request/verify, auto-register phone user; `ISmsSender` console stub
- [x] **Google**: ID-token verification, auto-link by verified email (disabled until client-id configured)
- [x] Admin-controlled providers: `GET /api/auth/config`, admin `GET/PUT /api/admin/auth-providers`
- [x] `AdminUserSeeder` sets the real BCrypt hash for the seeded admin
- [x] JWT bearer auth + `[Authorize(Roles=...)]`; verified end-to-end

### Frontend (✅ done — Angular 21 + Tailwind)
- [x] Login page renders dynamically from `/auth/config` (only enabled providers show)
- [x] Email/password + Mobile OTP (request→verify) flows
- [x] Google Sign-In wired (GSI ID-token → backend); shows only when enabled + client-id set — *untested (disabled by default)*
- [x] JWT storage (`TokenStorageService`) + functional interceptor + **silent refresh on 401**
- [x] Route guards (`authGuard`, `adminGuard`)
- [x] Admin screen to toggle providers (`/admin/auth-providers`)
- [x] App shell (header/nav), home landing, register page
- Verified: `ng build` clean; both servers run; CORS for `localhost:4200` confirmed.

### Follow-ups
- [ ] Email verification + password reset (needs `IEmailSender` — Stage 6 transactional email)
- [ ] Permission-based policies (currently role-based)

## Endpoints
`auth/config` · `auth/register` · `auth/login` · `auth/otp/request` · `auth/otp/verify` · `auth/google` · `auth/refresh` · admin `auth-providers`

## Key facts
- Admin: `admin@ecommerce.local` / **`Admin@123`** (change it).
- JWT key + Google secret → move to user-secrets before non-local use.

**Status:** 🟡 Backend complete & tested; Angular UI is the next task.
