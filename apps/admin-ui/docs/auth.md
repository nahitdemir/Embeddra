# Embeddra Authentication Reference

Embeddra Admin UI utilize a secure, hybrid authentication model combining **HttpOnly Cookies** for server-side route protection (Middleware) and **LocalStorage** for client-side API calls.

## Authentication Flow

### Unified Login
1. User submits **Email + Password** to `/login` (single login page).
2. Request is sent to Next.js API Route (`/api/auth/login`).
3. API Route calls backend `/auth/login` without `tenantId` (backend determines role).
4. Backend searches for user:
   - **Platform user**: Returns JWT token + user info (no tenantId)
   - **Tenant user (1 tenant)**: Returns JWT token + user info + tenantId
   - **Tenant user (>1 tenant)**: Returns `tenants` array (no token yet)
5. API Route sets `auth_token` as **HttpOnly Cookie** (if token returned).
6. Client stores token and user info in `localStorage` via `AdminSettings`.
7. **Automatic redirect based on role:**
   - **PlatformOwner** → `/platform`
   - **TenantOwner (1 tenant)** → `/tenant/{tenantId}`
   - **TenantOwner (>1 tenant)** → `/tenant/select`

### Route Protection
- **Middleware** (`middleware.ts`) intercepts all requests.
- Checks for `auth_token` cookie.
- **Protected routes** without cookie → Redirect to `/login`.
- **Login page** with cookie → Client-side redirect to appropriate dashboard.
- **Old login routes** (`/platform/login`, `/tenant/login`) → Redirect to `/login` (301).

### Configuration (Environment Variables)

All technical configurations are managed via `.env` files.

| Variable | Description | Default |
| :--- | :--- | :--- |
| `NEXT_PUBLIC_ADMIN_API_BASE_URL` | Base URL for the Admin API | `http://localhost:5114` |
| `NEXT_PUBLIC_SEARCH_API_BASE_URL` | Base URL for the Search API | `http://localhost:5222` |

## Route List

### Public Routes
- `/login`: Unified login page
  - If logged in: Auto-redirect to appropriate dashboard (client-side)
- `/health`: Health check endpoint (optional)

### Protected Routes (Platform)
- `/platform/**`: Requires `auth_token` cookie.
  - Without cookie → Redirect to `/login`
  - Requires `PlatformOwner` role (enforced by backend)

### Protected Routes (Tenant)
- `/tenant/select`: Tenant selection screen (Requires auth).
  - Without cookie → Redirect to `/login`
  - Only shown when user has access to multiple tenants
- `/tenant/[tenantId]/**`: Tenant-specific dashboard (Requires auth).
  - Without cookie → Redirect to `/login`
  - Requires user to have access to the specified tenant

### Redirect Rules
- `/` → `/login` (301 redirect)
- `/platform/login` → `/login` (301 redirect)
- `/tenant/login` → `/login` (301 redirect)

## Logout

Logout is handled via `Topbar` component's `handleLogout` function:

1. **Server-side**: Calls `/api/auth/logout` to clear `auth_token` cookie.
2. **Client-side**: Clears `localStorage` (token, user info, tenant presets).
3. **Redirect**: User is redirected to `/login`.
4. **Browser back guard**: Cache-control headers prevent returning to protected pages.

## Tenant Management

### Tenant Selection
- After login, if user has access to multiple tenants, `/tenant/select` screen is shown.
- User can search and select a tenant.
- Selection triggers login with selected tenantId, then redirects to `/tenant/{tenantId}` dashboard.

### Tenant Switcher
- In tenant panel topbar, if user has access to multiple tenants, a dropdown switcher is shown.
- Tenant list is fetched from `GET /auth/me/tenants` endpoint.
- User can switch between tenants without re-login.
- Switching updates `tenantId` in settings and redirects to `/tenant/{newTenantId}`.

## Backend Endpoints

### Authentication
- `POST /auth/login`: Login with email/password
  - Request: `{ email, password, tenantId? }`
  - Response: `{ token?, expires_at?, user?, tenants? }`
- `GET /auth/me`: Get current user info
  - Response: `{ tenant_id, role, is_platform, is_user, name, email }`
- `GET /auth/me/tenants`: Get user's tenant list
  - Response: `{ tenants: [{ tenant_id, tenant_name, email, role }] }`

### Role-Based Access
- **PlatformOwner**: Can access `/platform/**` routes
- **TenantOwner**: Can access `/tenant/{tenantId}/**` routes (where user has access)

## Security Notes

1. **HttpOnly Cookies**: Prevent XSS attacks on token
2. **LocalStorage**: Used only for client-side API calls (Bearer token)
3. **Middleware Protection**: All protected routes require cookie
4. **Browser Back Guard**: Cache-control headers prevent returning to protected pages after logout
5. **Role Validation**: Backend validates role on every request
6. **Tenant Access**: Backend validates user has access to requested tenant
