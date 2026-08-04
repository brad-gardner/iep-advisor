# Design Discussion: School Self-Serve Signup & Org Management

**Date:** 2026-06-06
**Feature:** Self-serve district signup, 3-tier org roles, invite-only staff, admin console, flag retirement — make the school side sellable.
**Origin:** `docs/brainstorms/2026-06-06-school-self-serve-signup-brainstorm.md`

## Current State

- **Registration** (`api/IepAssistant.Api/Controllers/AuthController.cs:80`, `AuthService.RegisterAsync`): data-annotation validation, beta-code gate, hardcodes `UserRole.Parent`, no JWT on register (bounce to `/login`). Frontend `web/src/features/auth/components/register-page.tsx` is a manual `useState` form (no form library anywhere in the app).
- **Educator entry**: only `POST /api/educator/onboard` (free-text find-or-create District+School, flips Parent→Educator) — reachable only by typing `/educator`. No UI link exists.
- **Org model**: `District → School → TeacherProfile(UserId, SchoolId required, Title, Credentials)`. No admin tier, no DistrictId on profile. Five services replicate a `TeacherProfiles → SchoolId` permission lookup (ChildLink, StudentInvite, StudentWorkspace, IepDraft, IepAssist/IepVersion).
- **Roster inconsistency**: `EducatorService.GetStudentsAsync` lists ALL school students; `GetStudentAsync` enforces per-student `SchoolStudentAccess`. (`EducatorService.cs:154-185`)
- **Invites**: token pattern (32-byte token → SHA-256 hash stored → atomic `ExecuteUpdateAsync` claim, 14-day expiry, email-bound) is **duplicated** in `ChildLinkService` + `StudentInviteService` (and ShareService). Emails actually send via `EmailService` (**Azure Communication Email**; logs instead of sending when `Email:ConnectionString` is empty). Links built from `App:FrontendUrl`.
- **Flags**: `IFeatureFlags` checked at ~25 backend call sites + `FeatureRoute`/`useFeatureFlag` at ~25 frontend sites (full inventory captured in research). All four default **off** in prod; on only in `appsettings.Development.json`.
- **Routing**: flat JSX `<Routes>` (no data router); per-route `ProtectedRoute → FeatureRoute → MainLayout`. Sidebar picks nav arrays by `user.role` + flags.
- **E2E**: `/e2e` Playwright package (17 specs, workers:1, shared storage-state auth, admin-driven test data via beta codes). No school-side specs. No CI run.
- **Wizard precedent**: `web/src/features/onboarding/components/onboarding-flow.tsx` (useState step + local `ProgressDots`).

## Patterns to Follow

- **Invite tokens**: reuse the ChildLink/StudentInvite pattern (hash-only storage, validate-before-claim, atomic `ExecuteUpdateAsync` claim) — `ChildLinkService.cs:83,211-236,396-407`. Extract the duplicated `Generate`/`HashToken` into one small static helper used by the new StaffInvite service (and retrofit the two existing services).
- **Enum-as-string**: `UserRole` tolerant string converter pattern for the new `OrgRole` enum.
- **Email**: add `SendStaffInviteEmailAsync` to `IEmailService`/`EmailService.cs` following `SendSchoolLinkInviteEmailAsync` (line ~100): `{FrontendUrl}/staff/accept-invite?token=...`.
- **Service results + controller mapping**: `ServiceResult<T>` wrappers, DTO mapping in controllers (`EducatorController.MapProfile`).
- **Tests**: SQLite-in-memory xUnit pattern (`StudentInviteServiceTests.cs:21-33`).
- **Wizard**: `onboarding-flow.tsx` step pattern; promote `ProgressDots` into `web/src/components/ui`.
- **Forms**: manual `useState` + shared `Input`/`Button`/`Notice` primitives, `data-testid` on every control (matches register-page).

## Desired End State

**Data model** (ERD delta):
```
OrgRoles        (new lookup table) Id, Name — seeded: DistrictAdmin, SchoolAdmin, Teacher
StaffProfile    (renamed from TeacherProfile)
                + OrgRoleId (required FK → OrgRoles)
                + DistrictId (required FK)        ← all staff belong to a district
                ~ SchoolId becomes nullable       ← null for DistrictAdmin
StaffInvite     (new) Email, DistrictId, SchoolId?, OrgRoleId (FK), InviteToken(hash),
                InviteExpiresAt, AcceptedAt, InvitedByUserId, IsActive
```
`UserRole` unchanged — all staff are globally `Educator` (brainstorm Approach A).

**Flows:**
1. `/register` → two-path chooser. Parent path = existing form (beta code kept). District path = account + district name + state → `POST /api/auth/register-district` creates User(Educator) + District + TeacherProfile(DistrictAdmin) in one transaction, **returns a JWT** (auto-login) → lands in first-run wizard: create first school → invite staff → done (each step skippable).
2. **Staff invite**: admin invites email + role + school → `StaffInvite` + real email. `/staff/accept-invite?token=` → recipient registers (name/password; email bound to token) → User(Educator) + TeacherProfile stamped from invite → atomic claim → **JWT minted immediately** (role correct from birth — the stale-JWT flip problem never fires). Email already on a parent/student account → clear rejection ("use your work email").
3. **Admin console** (same educator shell, nav expands by org role): `/educator/admin/schools` (list/create/edit), `/educator/admin/staff` (list/invite/revoke/deactivate), district overview on the educator dashboard. School Admin sees the school-scoped slice. Backed by a new `DistrictController` (org-role-checked in services, like existing SchoolId checks).
4. **Roster scoping**: teachers see only students with an active `SchoolStudentAccess` grant (fixes the list/detail inconsistency in the assigned-only direction); admins see their whole scope and **assign staff to students** (new endpoint granting `SchoolStudentAccess`).
5. **Player-coach**: a shared org-access helper answers "can user act on school S / student X" with admin-superset semantics; the five duplicated SchoolId checks migrate onto it.
6. **Educator self-onboard removed**: `POST /api/educator/onboard` + onboarding form deleted; `/educator` always shows the dashboard (profile guaranteed by signup/invite).
7. **Flags retired**: `IFeatureFlags`, `ConfigurationFeatureFlags`, `FeatureFlags`, `ConfigController`, `FeatureRoute`, `useFeatureFlag(Status)`, config-api — all deleted; ~50 call sites unconditionalized per the research inventory.
8. **E2E**: new specs — district signup → wizard → invite staff → accept → create student → assign → author; parent register; rejection paths. Plus updated existing specs where flag paths die.

## Design Decisions

1. **Org-role lives on TeacherProfile** (not a new membership table, not new UserRoles) — brainstorm Approach A; least churn, no stale-JWT risk.
2. **`OrgRole` as a DB lookup table** (user decision, overriding the enum recommendation) — `OrgRoles` table seeded with stable IDs (DistrictAdmin=1, SchoolAdmin=2, Teacher=3); a code-side `OrgRoleIds` constants class references them in permission logic.
3. **Rename `TeacherProfile` → `StaffProfile`** (user decision) — table rename migration + sweep of the 7 consuming services/DTOs, done now while the code is young.
4. **`DistrictId` denormalized onto TeacherProfile** — district admins have no school; avoids `School.District` joins in every authz check.
5. **`register-district` returns a JWT** (unlike parent register) — the first-run wizard must start immediately; no login bounce mid-sale.
6. **Staff invites are single-use, email-bound, 14-day, hash-stored** — identical guarantees to ChildLink/StudentInvite; `Generate/Hash` consolidated into a shared helper.
7. **District admins can invite all three roles** (incl. other district admins); school admins can invite SchoolAdmin/Teacher into their school only.
8. **E2E email capture**: when `Email:ConnectionString` is empty AND `Email:ExposeLinksForTesting=true` (dev/test config only, never prod), invite-creating endpoints include the invite URL in the response so Playwright can complete accept flows without an inbox.
9. **No IaC in scope** — ACS email config (`Email:ConnectionString`, `Email:SenderAddress`, `App:FrontendUrl`) is set in Azure App Service settings; documented as an ops checklist item in the plan, not Bicep.
10. **Beta gate kept for parents only** — `InviteCode` stays required on the parent path; district path has no code.

## Resolved Questions (user, 2026-06-06)

1. **`OrgRole`** → **DB lookup table** (decision 2 updated).
2. **Entity name** → **rename to `StaffProfile`** (decision 3 updated).
3. **Existing dev org data** → **wipe, don't migrate**: the migration deletes dev-created org rows (profiles, school students + dependents, links/invites) child-first via raw SQL and resets affected Educator users to Parent. Clean slate; no prod data exists.

## Testing Strategy

- **Unit (xUnit + SQLite in-memory):** register-district transaction; StaffInvite lifecycle (create/validate/expire/double-accept race/email-mismatch/parent-email rejection); org-access helper matrix (3 roles × school/student/cross-school/cross-district); roster scoping (teacher assigned-only vs admin full); assignment endpoint.
- **Playwright e2e:** golden path — district signup → wizard (school + staff invite) → staff accept → student create → assign teacher → teacher sees only assigned; parent two-path register; invite rejection (parent email); flag-retirement regression on existing specs.
- **Manual:** ACS email delivery against a real connection string; prod config checklist.
