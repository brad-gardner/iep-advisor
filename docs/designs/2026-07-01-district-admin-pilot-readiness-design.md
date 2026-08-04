# Design Discussion: District Admin Pilot Readiness

**Date:** 2026-07-01
**Feature:** Thin-slice pilot-readiness package — oversight dashboard, audit log viewer, invite-expiry notifications, cross-role polish pass
**Origin:** `docs/brainstorms/2026-07-01-district-admin-pilot-readiness-brainstorm.md`

## Current State

- **Admin home** (`web/src/features/educator/pages/educator-home-page.tsx`) shows a profile card, a 2-item `SetupChecklistCard`, and `DistrictOverviewCard` (`web/src/features/district-admin/components/district-overview-card.tsx`) — name, state, school/staff counts only. Fed by `GET /api/district` → `DistrictService.GetOverviewAsync` (three count queries).
- **Audit log is write-only.** `AccessAuditLog` (`api/IepAssistant.Domain/Entities/AccessAuditLog.cs`) has `Action` (enum stored as string: View/Edit/Share/Export/Finalize), `ActorUserId`, `ResourceType`, `ResourceId`, `RecipientUserId?`, `CreatedAt`. Rows are queued via singleton `AuditLogger` channel and drained by `AccessAuditLogWorker`. Indexes already fit reads: `(ResourceType, ResourceId, CreatedAt)` and `(ActorUserId)`. Zero read paths exist.
- **Invites can silently expire.** `StaffInvite` has `InviteExpiresAt` (14 days), status is derived (no column), and `InviteExpiresAt` is already exposed in `StaffPendingInviteDto`. There is **no reminder-sent tracking column** and no scheduled scan.
- **Email** = `EmailService` (Azure Communication Services), raw interpolated-HTML methods on `IEmailService` (6 methods). No-connection-string dev mode logs instead of sending; send failures are swallowed.
- **No aggregate queries exist** for "students with zero staff access grants" or "students with no linked parent" — but the join shapes exist (`StaffInviteService.BuildSolelyOwnedAsync`, `ChildLinkService` per-student linked checks).

## Patterns to Follow

- **Org authorization:** service-level checks via `IOrgAccessService.GetStaffContextAsync` (`api/IepAssistant.Services/Implementations/OrgAccessService.cs`); controllers stay bare `[Authorize]`. DistrictAdmin = district-wide, SchoolAdmin = own school.
- **Service result models → controller DTO mapping** as in `DistrictController` / `StaffController`.
- **Background work:** `BackgroundService` with per-item scope + swallow-and-log error handling (`api/IepAssistant.Api/BackgroundServices/AccessAuditLogWorker.cs`). The expiry scanner will be timer-based rather than channel-based, but follows the same registration and error-isolation style.
- **Email:** add methods to `IEmailService` following the `SendStaffInviteEmailAsync` shape (interpolated HTML + plaintext, private `SendEmailAsync`).
- **Frontend:** per-feature api module over `apiClient`, `useEffect` + `useState` with cancellation flag (no React Query), `Card` components with spinner/empty-state/`data-testid` conventions, client-side filters like `school-filter.tsx`.
- **Tests:** xUnit + real SQLite in-memory in `api/IepAssistant.Services.Tests` with `SeedDistrict/SeedStaff` helpers, `CapturingEmailService`, `CapturingAuditLogger`. E2E via `e2e/helpers/org-data.ts` API-driven fixtures.

## Desired End State

1. **Dashboard** — `GET /api/district/dashboard` (new `DistrictService` method) returns one aggregate payload: per-school active student counts, staff status summary (invited/active/deactivated — **no sign-in timestamps**, per brainstorm), pending invites with expires-at, count + list of students with **no active staff access grants**, count + list of students with **no linked parent** (`ChildLink` active + accepted). SchoolAdmin gets the own-school slice. Admin home renders these as tiles/cards linking into existing management pages; Teacher home unchanged.
2. **Audit viewer** — `GET /api/district/audit-log` (paged, filterable by staff member, student, action, date range) + a new "Activity log" page under the Administration sidebar group, visible to both admin tiers (SchoolAdmin scoped to own school). Rows enriched with actor name and resource display name (per-page dictionary lookups, no N+1). Read-only; no retention tooling.
3. **Expiry notifications** — nullable `ExpiryReminderSentAt` on `StaffInvite` (one migration). A timer-based `StaffInviteExpiryWorker` scans periodically for invites expiring within the warning window that are still pending and un-reminded, emails **the inviting admin only** (`InvitedByUserId`) via new `SendStaffInviteExpiringEmailAsync`, stamps the column. Idempotent by design.
4. **Polish pass** — a written golden-path script (admin signup → school → staff invite → student → access grant → parent invite → parent link → student invite) executed against the running app; empty states, copy, terminology, and email content fixed as found; ACS email config verified per `docs/ops/2026-06-07-school-launch-checklist.md`. The script doubles as the pilot onboarding runbook.

## Design Decisions

- **Audit entries scope to the district by ACTOR** (ActorUserId ∈ district staff profiles, active or inactive) — uses the existing `(ActorUserId)` index, matches the FERPA "who on our staff accessed what" accountability story, and avoids per-resource-type district joins. Parent/student actor activity is out of scope for this viewer. *(Open question 1 confirms this.)*
- **Single aggregate dashboard endpoint** rather than per-card fetches — fewer round trips, one loading state, one service method to test. Existing `DistrictOverviewCard` stays as-is.
- **"No assigned staff" = zero active `SchoolStudentAccess` rows** — admins have implicit player-coach access, so an access row is the meaningful signal that a working staff member owns the student.
- **"No linked parent" = no `ChildLink` with `IsActive && AcceptedAt != null && ChildProfileId != null`** — matches `ChildLinkService`'s existing definition of linked.
- **Reminder idempotency via `ExpiryReminderSentAt` timestamp** (not a separate table, not an enum) — one nullable column, one migration, resend resets it implicitly only if we choose to (default: resend creates fresh expiry, column stays; a resent invite whose new window nears expiry won't re-warn unless we null the stamp on resend — **we will null it on resend** so extended invites get a fresh warning).
- **Staff status shown as status only** (invited/active/deactivated) — decided during brainstorming; no last-sign-in timestamps.
- **Admin-only expiry email; both admin tiers see the audit viewer** — decided during brainstorming.
- **No new enum/lookup tables.** `AuditAction` (existing code enum, stored as string) is surfaced read-only; invite status stays derived; reminder tracking is a timestamp.
- **Server-side paging for the audit endpoint** (it grows unboundedly), even though rosters stay client-side — consistent with pilot scale everywhere else.

## Resolved Questions (design approved 2026-07-01 with proposed defaults)

1. **Audit scoping is actor-based** — the viewer answers "what did my staff access"; parent/student actor activity is out of scope for pilot.
2. **Expiry warning fires 3 days before expiry, daily scan** (worker also runs once on startup).
3. **Dashboard attention lists show up to ~5 names inline** with a "view all" link to the roster.

## Testing Strategy

- **Service tests** (SQLite in-memory, existing seed helpers): dashboard aggregates (per-school counts, zero-access and no-parent detection, SchoolAdmin slice), audit-log query (actor scoping, filters, paging, SchoolAdmin slice, Teacher rejected), expiry scan (window boundaries, idempotency, resend re-arms warning, `CapturingEmailService` assertions).
- **E2E (Playwright)**: dashboard tiles render for a seeded district (via `org-data.ts` fixtures); audit page shows an entry after a staff member views a student; role-visibility (Teacher sees neither page).
- **Manual**: the polish-pass golden-path script IS the manual verification, executed end-to-end against the dev stack with `Email:ExposeLinksForTesting`.
