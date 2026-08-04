# School Self-Serve Signup & Org Management — Brainstorm

**Date:** 2026-06-06
**Status:** Brainstorm complete, pending plan
**Goal:** Make the platform sellable to a school/district — self-serve org signup, discoverable school side, production-ready UX.

## What We're Building

A real front door and org-management layer for the school side, which today is structurally hidden (no UI path to `/educator`, all registration hardcoded to Parent, districts/schools created accidentally via free-text find-or-create, no admin tier).

1. **Two-path public registration** — one `/register` page: "I'm a parent" (existing flow, keeps beta invite code) or "I represent a school or district" (new, open — no beta code). District signup collects account + district name + state and lands the user as the district's first **District Admin**.
2. **Org roles (3 tiers)** — District Admin (whole district), School Admin (one school), Teacher (their students in one school). Modeled as an **org-role on the staff profile** (`TeacherProfile`-evolved); everyone stays `UserRole.Educator` globally — one shell, capabilities expand by org role.
3. **District/School admin console** — create/edit schools, invite staff (role + school carried on the invite), view/deactivate staff, district overview. School Admins get the school-scoped slice. No student oversight for admins day one.
4. **Invite-only staff joining** — staff join exclusively via email invite (reuses the proven SHA-token pattern from ChildLink/StudentInvite). The free-text educator self-onboard flow is **removed**.
5. **Real email** — Azure Communication Services for invite delivery (staff, parent-link, student invites), deployed with the existing Bicep/Azure stack.
6. **Feature flags retired** — `AnalysisRun`, `MeetingPrepStandalone`, `SchoolSide`, `StudentWorkspace` flag plumbing removed; the three-sided product is unconditionally on.
7. **Playwright e2e coverage** — new specs for the golden paths: district signup → create school → invite staff → teacher accepts → student/IEP flow; parent signup → link flow. Harness already exists (`/e2e`, 17 specs, admin-driven test data helpers).

## Why This Approach

- **Self-serve signup** doubles as the demo and the provisioning tool — closest path to "try and start selling it."
- **Org-role-on-profile (Approach A)** beats new global `UserRole` values (B) and a generic membership table (C): least churn to 9 phases of working code, one shell, and it sidesteps the known stale-JWT-on-role-flip problem entirely (role is set at invite-accept registration, never flipped). C stays reachable later.
- **Invite-only** kills the junk-district/free-text-dedup problem at the root and matches how schools actually procure.
- **Retiring flags** removes dual-path testing burden; the flags did their job during phased development.

## Key Decisions

| Decision | Choice |
|---|---|
| Sales motion | Self-serve district signup (public) |
| Org roles | District Admin / School Admin / Teacher |
| Role modeling | Org-role on staff profile; global role stays `Educator` (Approach A) |
| Staff joining | Invite-only (email, role+school on token); free-text self-onboard removed |
| Front door | Two-path `/register` (parent vs. school/district) |
| Beta gate | Kept for parents only; district signup open |
| Single-role accounts | Kept — staff invite to an email holding a parent account is rejected with a clear message (use work email) |
| Admin console scope | Org + staff management only; no cross-student oversight day one |
| Feature flags | All four retired (code paths unconditional) |
| Email | Real sending via Azure Communication Services |
| Existing data | No prod data — old free-text-created districts/schools are dev-only; no migration concern |

## Known Issues to Fix In-Scope

- **Stale JWT after role change**: mitigated by design (roles set at registration), but invite-accept must mint a fresh token rather than rely on `refreshUser()`.
- **`GetStudents` vs `GetStudent` inconsistency**: list shows the whole school while detail enforces per-student `SchoolStudentAccess` — align both to per-student access grants (see Resolved Questions).
- **Prod config**: school-side endpoints currently 404 in production; flag retirement resolves this.

## Resolved Questions

1. **Player-coach admins** — **Yes.** Admin roles are a superset: School Admin can do everything a Teacher can within their school; District Admin within any school in the district. No "wrong role" dead ends.
2. **First-run experience** — **Guided onboarding wizard.** District signup flows straight into: create your first school → invite your first staff → done. Each step skippable.
3. **Teacher roster visibility** — **Only assigned students.** Teachers see students they created or were granted access to; admins see the whole school/district roster and assign teachers. Tighter FERPA posture. (Fixes the existing list/detail inconsistency in the "assigned-only" direction.)

## Out of Scope

- Multi-role accounts (parent + staff on one email) — revisit if a customer hits it
- Admin student/IEP oversight dashboards
- Request-to-join / school directory search
- Billing/subscription for districts
- Custom email domain (ACS managed domain first)
