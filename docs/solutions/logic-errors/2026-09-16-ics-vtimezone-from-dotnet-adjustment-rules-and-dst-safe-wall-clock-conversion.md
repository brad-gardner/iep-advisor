---
module: "Meetings"
date: "2026-09-16"
problem_type: "logic_error"
component: "service_object"
symptoms:
  - "Generated VTIMEZONE for America/New_York said DAYLIGHT starts BYDAY=1SU in March (US DST is the second Sunday) and STANDARD DTSTART read T015959"
  - "Scheduling a meeting at 06:30 local on the US spring-forward date stored an instant that rendered back as 07:30"
  - "Emailed reschedule/cancel .ics attachments always carried SEQUENCE:0 while the API's .ics carried the bumped value"
  - "Calendar month grid put an evening Pacific-time meeting on the next day because it bucketed by the UTC date"
root_cause: "logic_error"
dotnet_version: "9.0"
resolution_type: "code_fix"
severity: "high"
tags: [icalendar, rfc5545, vtimezone, timezoneinfo, dst, intl, react, calendar, sequence, code-review]
---

# ICS VTIMEZONE from .NET adjustment rules, and DST-safe wall-clock → UTC conversion in the browser

## Problem
Plan 4 added meetings with per-meeting `.ics` attachments and a per-user calendar feed (backend
`IcsBuilder` on .NET 9) and a schedule form that converts a wall-clock date/time in an IANA zone to
UTC (web, `Intl` only, no library). Both shipped with time-zone logic that was right on the days the
tests covered and wrong on the days that matter.

## Root causes

1. **`TimeZoneInfo` adjustment rules are not floating rules on Linux/macOS.** For IANA zones the
   runtime returns one `AdjustmentRule` *per year* with `IsFixedDateRule = true`; `TransitionTime.Week`
   and `.DayOfWeek` are leftover defaults (Week=1, Sunday), not the rule. Emitting
   `RRULE:…;BYDAY={Week}{Day}` from them produced `1SU` for a second-Sunday transition. The transition
   `TimeOfDay` for the standard rule was also `01:59:59.999`, rendered as `T015959`.
2. **Single-sample offset conversion.** `zonedDateTimeToUtcIso` sampled the zone offset once at a
   "guess" instant (the wall clock read as UTC) and applied it. Within the hours after a transition the
   guess sits on the other side of the change, so the result is off by exactly the DST delta.
3. **Two mappings for one fact.** `NotificationEmailService` rebuilt the ICS input for emails with a
   hard-coded `Sequence = 0` while `CalendarService` used the real value — iTIP clients use SEQUENCE
   to decide whether an update is newer.
4. **Bucketing by the wrong calendar.** `CalendarItemDto.date` is the UTC instant for meetings; the
   grid sliced `date.slice(0,10)` (the UTC day) while its cells were built from the browser's local day.

## Fixes

- `IcsBuilder`: resolve the concrete transition date first (`ComputeTransitionDate`, which already
  handled fixed-date rules), then derive BYDAY from it — ordinal `(day-1)/7+1`, or `-1` when the date is
  the last such weekday of its month — and the weekday from the resolved date. Round the transition
  time to the second. Only choose a DST rule that covers a year near "now" (Phoenix carries a 1967
  rule that would otherwise emit a bogus DAYLIGHT block). Tests: New York `BYMONTH=3;BYDAY=2SU` /
  `BYMONTH=11;BYDAY=1SU`, London `-1SU`/`-1SU`, Phoenix standard-only, `T020000`.
- `meeting-time.ts`: iterate — compute the instant with the offset at the guess, re-sample the offset
  at that instant, repeat until it converges (≤3 samples); shift forward through spring-forward gaps;
  resolve fall-back ambiguity to the earlier occurrence. Regression tests for 00:00–07:00 local on
  2026‑03‑08 and 2026‑11‑01 (America/New_York) and Europe/Berlin's spring gap, all asserting a
  round-trip through `Intl`.
- One `IcsMeetingInputMapper` shared by the feed/endpoint and the email worker; the emailed cancel
  `.ics` now carries `SEQUENCE:{n}`.
- `calendar-item-date.ts`: meetings bucket by `utcIsoToZonedParts(startsAtUtc, browserTimeZone()).date`;
  obligations (date-only, UTC midnight by construction) keep the slice.

## Verification
- `dotnet test` 697 (was 672); `vitest` 290 (was 262); live QA feed shows the corrected VTIMEZONE.
- Review: pass 1 (8 specialists, Sonnet — Opus weekly limit) 7 P1 / 20 P2 / 13 P3 → fixed in
  `b697be8`; passes 2–4 surfaced 7 more P2 (VideoUrl scheme, reschedule dropping invitees, admin digest
  scope, SQLite collision classifier, bell focus, RSVP error handling, per-child error reset) → fixed in
  `c9f94ec`/`ce38fa2`; final 0 P1 / 0 P2 / 10 P3 (`todos/071–072`).

## Prevention
- Never read `TransitionTime.Week/DayOfWeek` without checking `IsFixedDateRule`; derive from a
  resolved date. Test at least one northern zone, one `-1SU` zone, one no-DST zone, and assert the
  RRULE text.
- Any wall-clock → instant conversion must be verified by converting back and comparing; test the
  hours after both transitions, not 14:00 in January and July.
- When two code paths render the same calendar object, share the mapper (the "email copy" drifts).
- Bucket instants by the viewer's calendar, not by the ISO string's date part.

## Related
- Plan: `docs/plans/2026-09-15-004-feat-meetings-deadlines-notifications-calendar-plan.md`
- Other pass-1 fixes in the same commit: minimal token-RSVP payload, HTML-encoded email templates,
  single-query meeting lists, batched digest/reminder workers, email backoff (`NextAttemptAt`).
- P3 follow-ups: `todos/071-*`, `todos/072-*`
