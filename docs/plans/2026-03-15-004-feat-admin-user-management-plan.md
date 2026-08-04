---
title: "feat: Admin User Management UI"
type: feat
status: active
date: 2026-03-15
---

# feat: Admin User Management UI

## Overview

Build a frontend admin panel for managing users. The backend already exists — `UsersController` with `[Authorize(Roles = "Admin")]`, `UserService` with CRUD, and `User.Role` field (default "User", can be "Admin"). This is purely a frontend feature.

To make a user an admin: update their `Role` to "Admin" in the database directly (or via the existing `PUT /api/users/{id}` endpoint once another admin exists). A seed SQL can set the first admin.

## What Already Exists (Backend)

- `User.Role` field: `string`, defaults to `"User"`, stored in JWT claims
- `UsersController`: `GET /api/users` (list all), `GET /api/users/{id}`, `PUT /api/users/{id}`, `DELETE /api/users/{id}` — all `[Authorize(Roles = "Admin")]`
- `UserService`: `GetAllUsersAsync`, `GetUserByIdAsync`, `UpdateUserAsync` (can change role, isActive), `DeleteUserAsync` (soft delete)
- `UpdateUserRequest` DTO: firstName, lastName, state, role, isActive
- JWT includes `ClaimTypes.Role` — ASP.NET Core `[Authorize(Roles = "Admin")]` works out of the box

## What Needs to Be Built (Frontend Only)

### New files:

| File | Description |
|------|-------------|
| `web/src/features/admin/api/admin-api.ts` | API client for admin endpoints |
| `web/src/features/admin/hooks/use-users.ts` | Hook to fetch user list |
| `web/src/features/admin/components/admin-users-page.tsx` | User list with search, role/status badges |
| `web/src/features/admin/components/admin-user-detail.tsx` | User detail with edit form (role, active status) |
| `web/src/features/admin/components/admin-route-guard.tsx` | Route guard checking `user.role === 'Admin'` |

### Modified files:

| File | Change |
|------|--------|
| `web/src/types/api.ts` | Add `AdminUser` type with all user fields |
| `web/src/components/layouts/sidebar.tsx` | Show "Admin" nav section (Shield icon) only when `user.role === 'Admin'` |
| `web/src/app/routes.tsx` | Add `/admin/users` and `/admin/users/:id` routes wrapped in admin guard |

### Admin Users Page

- Lora H1: "User Management"
- Search input (filter by email/name)
- Table with columns: Name, Email, Role (badge), Status (Active/Inactive badge), Joined date
- Click row → navigate to user detail
- Brand styling throughout

### Admin User Detail

- User info display
- Editable fields: Role (dropdown: User/Admin), Active status (toggle)
- "Save" button calls `PUT /api/users/{id}`
- "Deactivate" button calls `DELETE /api/users/{id}`
- Back link to user list

### Admin Route Guard

Simple component that checks `useAuth().user?.role === 'Admin'` and redirects to dashboard if not admin. Wraps admin routes.

### Sidebar Admin Section

Only visible when `user.role === 'Admin'`:
```
─── Admin ───
  Users (Shield icon)
```

### Seeding the First Admin

Add a SQL script or migration that sets a specific user as Admin:
```sql
UPDATE Users SET Role = 'Admin' WHERE Email = 'brad@example.com'
```

Or expose this as a one-time setup instruction in the README.

## Acceptance Criteria

- [ ] Admin users see "Admin" section in sidebar with "Users" link
- [ ] Non-admin users do NOT see the admin section
- [ ] Admin users page lists all users with role/status badges
- [ ] Admin can search/filter users by name or email
- [ ] Admin can view user details
- [ ] Admin can change a user's role (User ↔ Admin)
- [ ] Admin can deactivate/reactivate a user
- [ ] Non-admin users are redirected if they try to access `/admin/*` routes
- [ ] Brand UI components used throughout

## Dependencies & Risks

**Dependencies:** None — backend already exists

**Risks:** Low — purely frontend CRUD UI calling existing endpoints

## Sources & References

### Internal References
- Users controller: `api/IepAssistant.Api/Controllers/UsersController.cs`
- User service: `api/IepAssistant.Services/Implementations/UserService.cs`
- User entity: `api/IepAssistant.Domain/Entities/User.cs`
- Auth context: `web/src/features/auth/stores/auth-context.tsx`
