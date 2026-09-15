---
title: "feat: Meetings, procedural deadlines, notifications and staff calendar"
type: feat
status: active
date: 2026-09-15
origin: docs/gap/combined-findings.md
slicing_approach: vertical
series: school-sale-readiness 4/8
depends_on: docs/plans/2026-09-15-003-feat-student-roster-xlsx-import-iep-teams-plan.md
---

# feat: Meetings, deadlines, notifications, calendar (combined findings C07)

## Problem Statement

There is no meeting entity, no procedural deadline model, no notification model and no calendar. `MeetingPrepChecklist.MeetingDate` is a parent prep field; `StaffInviteExpiryWorker` is the only date-driven job; `EmailService` only sends invites/resets. Without these, homes/dashboards (plan 5) have nothing to show, the parent loop (plan 6) has no trigger, and the buyer's compliance story (P7) does not exist.

## Decisions (defaults)

1. **Meeting** `{ SchoolStudentId, Type enum (AnnualReview|InitialIep|Amendment|EtrEligibility|Reevaluation|Transition|ManifestationDetermination|Other), Title, StartsAtUtc, TimeZoneId (IANA, default district tz "America/New_York"), DurationMinutes, Location, VideoUrl, Status enum (Proposed|Scheduled|Held|Continued|Cancelled), DocumentInstanceId?, Notes, CreatedByUserId }` + `MeetingParticipant { MeetingId, UserId?, ExternalName?, ExternalEmail?, TeamRole, IsRequired, InviteStatus (Pending|Accepted|Declined|Tentative), Attended?, ExcusedAt?, ExcusalNote }`.
2. **Deadlines are derived, not duplicated:** `StudentObligation` is a computed read model from `SchoolStudent.{AnnualReviewDueDate, ReevaluationDueDate, EtrDate}` plus `EvaluationCase` (plan 7) — this plan computes from the student dates only. Each obligation: `{ kind, dueDate, sourceLabel, ownerUserId (lead case manager), status: Upcoming|DueSoon(30d)|Overdue|Unknown (no date) }`. Rules live in `ObligationRules` with an `OH` profile (annual ≤ 365 days, re-eval ≤ 3 years) and a default profile; versioned by constant, not hardcoded in UI.
3. **Notifications:** `Notification { UserId, Kind, Title, Body, LinkPath, CreatedAt, ReadAt?, EmailSentAt?, EmailError? }` + `INotificationService.NotifyAsync(userIds, kind, payload)`. In-app bell + list; email via `EmailService` for kinds flagged `emailImmediately` (meeting scheduled/changed/cancelled, draft shared, response received) and a **daily digest worker** (07:00 district tz) for upcoming/overdue obligations and meetings in 7 days. De-dup by `(UserId, Kind, DedupKey)` within 24h. Email failures are recorded on the row (`EmailError`) and visible to platform admin — no silent no-op.
4. **Calendar:** `/educator/calendar` month/week list view of my meetings (participant) + my caseload obligations (lead). **ICS:** `GET /api/calendar/feed/{token}.ics` per user (revocable token on `User.CalendarFeedToken`), plus per-meeting `.ics` attachment in the invitation email (`METHOD:REQUEST`; updates bump `SEQUENCE`). No two-way sync claimed.
5. **Parents** see meetings for their linked child (`/api/children/{id}/meetings`) and receive scheduled/changed emails; accept/decline via link.
6. **Reminder worker:** T-7d, T-1d, T-1h in-app + email for participants; runs every 15 min; idempotent via `MeetingReminder { MeetingId, UserId, Offset, SentAt }`.

## Proposed Solution

### Backend
- Entities + migrations: `Meeting`, `MeetingParticipant`, `MeetingReminder`, `Notification`, `User.CalendarFeedToken`.
- `MeetingService`: create (defaults participants from the student team + linked parents + student account), update/reschedule (SEQUENCE++), cancel, set status, attendance, `ListForUser(range)`, `ListForStudent`. Authz: Collaborator+ on student to create; participants read.
- `ObligationService.GetForUser(userId)` / `GetForDistrict(scope)` computing from student dates; `GetForStudent`.
- `NotificationService` + `NotificationsController` (`GET /api/notifications?unread`, `POST /{id}/read`, `POST /read-all`), `NotificationEmailWorker` (drains queued emails with retry ×3), `DigestWorker`, `MeetingReminderWorker`.
- `CalendarController`: feed + per-meeting ics; `IcsBuilder` (RFC 5545 minimal: VEVENT, DTSTART/DTEND with TZID, UID = `meeting-{id}@iep-advisor`, SEQUENCE, STATUS, ORGANIZER, ATTENDEE).
- `EmailService`: `SendMeetingInvitationAsync` (with ics attachment), `SendMeetingUpdatedAsync`, `SendMeetingCancelledAsync`, `SendNotificationAsync(generic)`, `SendDigestAsync`. Surface send failures as exceptions to the worker (no swallow) — worker records `EmailError`.

### Web
- Student detail: "Meetings" card (upcoming + history), "Schedule meeting" modal (type, date/time with tz, duration, location/video, participants pre-checked from team/family, required toggles).
- `/educator/calendar`: month grid + agenda list; click → meeting detail drawer (participants, RSVP state, attendance capture for Held, reschedule/cancel, copy ICS link).
- Notification bell in the sidebar header with unread count, dropdown list, `/notifications` page; profile page: "Calendar subscription" with copy/regenerate URL.
- Parent: child overview "Upcoming meeting" card; accept/decline; notifications bell shared.
- Timeline card on student detail: obligations with status chips and "Edit dates".

## Implementation Phases

### Phase 1 — Meetings + participants + student/calendar UI (vertical)
- Entities, service, controller, schedule modal, meetings card, calendar page (no email yet). Tests: participant defaults, authz, reschedule bumps sequence, cancel.
- Checkpoint: schedule a meeting, see it on the SLP's calendar, reschedule it.

### Phase 2 — Notifications + email + ICS
- Notification entity/service/UI; email methods; ICS builder; invitation/update/cancel emails with attachment; feed endpoint + profile UI; parent RSVP link.
- Tests: ICS validity (UID stable, SEQUENCE increments, TZID), dedup, email failure recorded, feed token revocation.
- Checkpoint: invitation email (dev log) includes a valid .ics; parent accepts via link.

### Phase 3 — Obligations + reminders + digest
- `ObligationService` + rules, timeline card, reminder worker, digest worker, unknown-date handling.
- Tests: OH rules (annual/re-eval), status buckets, unknown date → Unknown not Healthy, reminder idempotence, digest content.
- Checkpoint: a student with annual review due in 20 days appears as DueSoon for the lead and in the digest.

## Acceptance Criteria

- [ ] Meetings can be scheduled with typed participants (staff/family/student/external), rescheduled, cancelled, marked held with attendance recorded separately from RSVP.
- [ ] Staff calendar shows my meetings and caseload obligations; per-user ICS feed and per-meeting ICS attachments validate in Google Calendar/Outlook import.
- [ ] Parents see and RSVP to their child's meetings.
- [ ] Obligations computed from student dates with OH/default rules; missing dates show as Unknown; owner = lead case manager.
- [ ] In-app notifications with unread count; immediate emails for meeting/draft/response events; daily digest; T-7/T-1/T-1h reminders; email failures visible, retried.
- [ ] All checks pass.

## System-Wide Impact

- `EmailService` currently swallows failures — this plan changes the contract for new methods (throw) and the worker records errors; existing invite methods unchanged.
- Time zones: store UTC + IANA tz; render in the viewer's browser tz with the meeting tz label.
- Plan 5 consumes `ObligationService`, `MeetingService.ListForUser`, `Notification` counts.

## Dependencies & Risks

- Depends on plan 3 student dates + team + parent links.
- ACS email in QA may be unconfigured — dev mode logs; tests use `CapturingEmailService`.

## Sources

- Origin: [combined-findings](../gap/combined-findings.md) C07; J6 meeting day; P6 Dennis; P7 Karen timelines
- RFC 5545 iCalendar; Ohio Operating Standards (annual review within 12 months; re-evaluation at least every 3 years)
- Code: `EmailService.cs`, `StaffInviteExpiryWorker.cs`, `MeetingPrepChecklist.cs`, `sidebar.tsx`
