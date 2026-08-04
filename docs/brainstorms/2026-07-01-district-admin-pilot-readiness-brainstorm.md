# District Admin Pilot Readiness — Brainstorm

**Date:** 2026-07-01
**Status:** Draft
**Precedes:** `/sht:plan`
**Builds on:** School Self-Serve Signup (PR #19, shipped 2026-06-07); prior brainstorm `2026-06-06-school-self-serve-signup-brainstorm.md`

## Context

Preparing for a **mixed pilot**: district admins, their staff, and parents/students testing together. Pilot orgs are small (≤20 students each), so one-at-a-time entry is acceptable and bulk import / server pagination are deferred. The bar is "all of the above": complete core loops, polish + trust signals, and coverage of admin expectations.

Repo research confirmed the org foundation is solid (signup wizard, schools CRUD, staff invites with revoke/resend/deactivate, roster scoping, assigned-staff panel, parent/student invite bridges — no stubs or dead ends). The admin's *reason to log in after week one* is weak: the home page is a thin profile card, the FERPA access-audit log is captured but invisible, invites can silently expire, and the cross-role chain hasn't had a deliberate polish pass.

## What We're Building

A thin-slice pilot-readiness package across four areas:

1. **Oversight dashboard (admin home)** — replace the thin `/educator` home for admins with district-wide tiles derived from existing data: students by school, staff roster with last activity, pending/expiring invites, students with **no assigned staff**, students with **no linked parent**. Tiles link into the existing management pages. No new tracking infrastructure.
2. **Audit log viewer** — a read-only, filterable page (student, staff member, date range) over the existing append-only `AccessAuditLog`. One query endpoint + one page. This is a FERPA trust signal schools will ask about, and the data is already being written.
3. **Lifecycle notifications (transactional only)** — email when a staff invite is nearing expiry (invites are single-use, 14-day), and when key accept events happen (staff invite accepted, parent link accepted). Extends the existing 6-method `IEmailService`; no digest engine, no in-app notification center.
4. **Cross-role polish pass** — scripted end-to-end walkthrough of the pilot's golden path (admin signs up → creates school → invites staff → staff adds student → assigns access → invites parent → parent links → student invited), fixing empty states, error copy, terminology consistency, and email content as found. Includes clearing the launch-checklist stragglers relevant to pilot (ACS email config verification, prod settings audit).

## Why This Approach

- For a mixed pilot with small rosters, **breadth of trust signals beats depth in any one feature**. A deep IEP-timeline compliance dashboard (Approach B) needs data quality the model doesn't reliably hold yet and would ship alongside unpolished cross-role edges.
- Every item builds on data or infrastructure that already exists (audit rows, invite expiry timestamps, org counts, email service) — low speculative risk, honest YAGNI.
- The oversight dashboard answers the admin's dual job-to-be-done (facilitator + monitor) at pilot scale; the deeper compliance/oversight product is the natural post-pilot fast-follow once real usage shows which metrics matter.

## Key Decisions

- **Pilot audience:** mixed (admins + staff + parents/students together); several small orgs (≤20 students each).
- **Deferred explicitly:** bulk CSV import, server-side roster search/pagination, SSO (SAML/Clever/ClassLink), district billing/seats, in-app notification center, IEP-timeline compliance dashboard, multi-role accounts, SIS integration.
- **Dashboard = admin home**, not a separate route; DistrictAdmin sees district-wide, SchoolAdmin sees own-school slice (consistent with existing `OrgAccessService` player-coach scoping).
- **Audit viewer is read-only** over existing `AccessAuditLog`; no new write paths, no retention/deletion tooling for pilot.
- **Notifications stay email-only**, extending `IEmailService`; scheduled work (expiry scan) can follow the existing background-worker pattern (`AccessAuditLogWorker`).
- **Polish pass is scripted, not incidental** — a written golden-path script doubles as the pilot onboarding runbook.

## Resolved Questions

1. **Invite-expiry warnings go to the admin only.** The admin decides whether to resend; the dashboard's pending-invites tile reinforces this, and invitees who chose not to join aren't nagged.
2. **Audit viewer is visible to both admin tiers** — DistrictAdmin sees the whole district, SchoolAdmin sees own-school entries, consistent with existing player-coach scoping in `OrgAccessService`.
3. **Staff activity tile shows status only** (invited / active / deactivated) — no sign-in timestamps, avoiding surveillance optics during a trust-building pilot. Timestamped adoption metrics can be a post-pilot addition if admins ask.

## Out of Scope

Everything in the "Deferred explicitly" list above, plus: fixing the 8 pre-existing e2e drift failures (tracked separately in project follow-ups), IepVersion DB-trigger immutability, and the parent-side `ChildProfile.SchoolDistrict` free-text → District FK reconciliation (real gap, but not pilot-blocking for orgs onboarded school-side).
