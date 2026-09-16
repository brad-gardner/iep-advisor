# Pilot Golden-Path Runbook

**Date:** 2026-07-01
**Purpose:** Doubles as (1) the manual verification script for the district-admin pilot-readiness feature and (2) the pilot onboarding runbook handed to a new school/district. Walk it end-to-end against a running dev stack before inviting pilot users.
**Related:** plan `docs/plans/2026-07-01-001-feat-district-admin-pilot-readiness-plan.md`; launch checklist `docs/ops/2026-06-07-school-launch-checklist.md`.

## Pre-flight (dev stack)

- [ ] API running: `cd api && ASPNETCORE_ENVIRONMENT=Development dotnet run --project IepAssistant.Api --no-launch-profile --urls https://localhost:7200`.
- [ ] Web running at `https://localhost:5200` (`cd web && npx vite --port 5200`) — the value of `App:FrontendUrl`; invite links are built from it, so `5173` will not match.
- [ ] `Email:ExposeLinksForTesting = true` in dev so invite URLs surface in API responses (set in `appsettings.Development.json`). Outbound email is queued in `OutboundEmails`; without ACS the worker logs "would be sent" and the row is marked Sent. Failures are visible at `/admin/email`.
- [ ] DB migrations applied (`dotnet ef database update --project IepAssistant.Domain --startup-project IepAssistant.Api` from `api/`).
- [ ] **Demo district (optional, recommended for a demo):** `dotnet run --project IepAssistant.Api -- seed-demo` (add `--reset` to wipe and recreate). Prints the logins; every account uses the printed password. Refused in Production.

## Happy path

Run as a brand-new district (fresh email each pass — the flow is one-district-per-signup).

1. **District signup** — `/register` → choose *District* → fill name/email/district/state/password → submit. Expect auto-login and redirect to `/educator/setup`.
2. **Setup wizard** — welcome → **create school** (name + state) → **invite staff** (enter an email, pick a role + school) → confirm the copyable invite URL appears (`/staff/accept-invite?token=…`) → finish. Land on `/educator`.
3. **Oversight dashboard (NEW)** — on `/educator`, confirm the four tiles render: **Schools** (per-school student counts), **Staff** (active / invited / deactivated), **Invites** (the pending invite, with expiry date), **Needs attention**. With no students yet, the attention tile shows its setup-oriented empty state; the pending invite shows in both the Staff *invited* count and the Invites tile.
4. **Accept the staff invite** — open the invite URL in a fresh/incognito context → accept, set a password → the new staff user lands on their educator home. Back as the admin, refresh `/educator`: Staff *active* count increments, *invited* decrements.
5. **Add a student** — `/educator/students` → create a student (as DistrictAdmin, pick the school). Confirm it appears on the roster and in the dashboard **Schools** tile count. The student now shows on the **Needs attention** → *no assigned staff* and *no linked parent* lists (distinguished "Not invited").
6. **Assign staff to the student** — open the student detail → assigned-staff panel → grant the staff member access. Refresh `/educator`: the student drops off the *no assigned staff* list.
7. **Invite a parent** — from the student detail, invite a parent (email). The *no linked parent* row for that student flips to **"Invite pending"**. Open the parent link in a fresh context → accept → the student drops off the *no linked parent* list entirely.
8. **Activity log (NEW)** — as the staff member, open/view the student's IEP draft (creates an audit "View"). As the admin, open **Administration → Activity log** (`/educator/admin/activity`): confirm the staff member's activity appears with a human action verb, the staff member's name, and the student/resource name. Exercise the filters (staff member, action, date range) and **Load more** if there are enough rows.
9. **Attention deep-link** — on the dashboard, click a **Needs attention → View all** link; confirm it lands on `/educator/students?attention=no-staff` (or `no-parent`) with the filter indicator banner, showing only the matching students, and **Clear** restores the full roster.

10. **Roster import** — `/educator/students` → **Import** → upload the XLSX template (see `docs/solutions/performance-issues/2026-09-15-set-based-xlsx-import-…`) → preview → commit. Rows land with external ids, grade/disability enums and timeline dates; errors are per-row.
11. **Author and finalize** — student → **Documents** → *New document* (OH IEP) → the editor prefills from the evidence bundle (prior versions, ETR, parent contributions) → AI assist inserts cited suggestions → **Finalize**. The version renders as an OH form-style PDF (PR-07 header, numbered sections, "Not addressed", goal blocks, signature blocks) and projects **goal records**.
12. **Share with the family** — editor → **Share with family** → the linked parent sees the frozen revision at `/children/{id}/shared-drafts/{rev}` with Explain / Ask (private) / Respond; staff see responses in the **Converge** tab and resolve them.
13. **Meeting** — student → **Meetings** → schedule (default participants, ICS, reminders) → **Brief** (summary, changes, resource commitments, procedural checklist) → mark **Held** → record **decisions** → the editor lists them as **proposed edits** → **Family summary** drafted, edited, sent.
14. **Progress** — student → **Goals** → *Log progress* (60 seconds) → the trajectory sparkline updates; stale goals (45 days) surface on the home obligation list.
15. **Record** — version detail → **Attach signed PDF** (status → Signed) → **Amend** (opens a prefilled draft; finalize records the chain) → student **Export record** → `/educator/admin/exports` → download the ZIP (manifest with sha256; private parent notes never included).

## Unhappy branches (must also pass)

- [ ] **Accept an expired invite** — take an invite whose window has passed (or manually age `InviteExpiresAt`); the accept page must show a clear "invite expired" message, not a crash or silent failure. This is the reason Phase 3 exists — confirm the dashboard **Invites** tile flags such invites as *Expired* and they remain resendable.
- [ ] **Revoked invite link** — revoke a pending invite from `/educator/admin/staff`, then open its link → expect a clear "no longer valid" message.
- [ ] **Staff invite to an existing parent email** — inviting staff to an email already registered as a parent account must be rejected with a clear message (single-role accounts).
- [ ] **Expiry reminder email** — with a pending invite ~3 days from expiry, confirm the `StaffInviteExpiryWorker` emails the **inviting admin** (not the invitee); resending the invite re-arms exactly one future warning. In dev (no ACS connection string) this logs "Email would be sent to …" — confirm the recipient is the admin's address.
- [ ] **Role visibility** — a Teacher sees neither the "Activity log" nav item nor the dashboard admin tiles; direct navigation to `/educator/admin/activity` is denied.

## ACS email config verification (carried from launch checklist)

All outbound email is queued and sent by `OutboundEmailWorker`; without ACS configured outside Development the API logs a startup **warning** and `/admin/email` shows *Email delivery is not configured*. Before pilot, verify in the target environment:

- [ ] `Email__ConnectionString` set (ACS resource).
- [ ] `Email__SenderAddress` set and verified on the ACS domain.
- [ ] `App__FrontendUrl` points at the real pilot web origin (invite + reminder links are built from it).
- [ ] Send one real staff invite and confirm delivery (check spam).

## Findings triage

Record issues found during the walkthrough here, split **blocking** (fix before pilot) vs **follow-up** (log and proceed). Static polish-audit findings and their resolutions are captured in the plan; live-walkthrough findings go below.

**Static polish audit (2026-07-01) — resolved before pilot:**
- Dashboard no longer vanishes on load failure — shows an error card with a "Try again" retry.
- Loading spinners on the dashboard, activity log, and roster now expose `role="status"` + screen-reader text.
- Audit action-filter labels now read past-tense ("Viewed"/"Exported") to match the row phrasing.
- Attention empty-state copy ("any student…"), Schools empty-state copy, and the doubled "Expired" on expired-invite rows tightened.
- Educator dashboard card no longer repeats the district name for a DistrictAdmin.
- Attention-filter indicator text darkened (amber-600) for contrast.
- _(Also fixed: the audit-log Teacher-visibility e2e assertion, which had targeted the wrong element.)_

**Live-walkthrough findings:**
- _Blocking:_ (none recorded yet — pending live-stack run)
- _Follow-up:_ (none recorded yet — pending live-stack run)
