---
module: "Dashboards"
date: "2026-09-16"
problem_type: "best_practice"
component: "service_object"
symptoms:
  - "Compliance-board tiles and the roster rows they linked to could disagree once a non-default date range was chosen"
  - "A staff member removed from a student's team kept seeing that student's draft on their home page"
  - "Due-date indexes added for the board were never used because the predicate wrapped the column in COALESCE"
  - "The district admin's home summary and the compliance board computed the same six numbers independently"
root_cause: "logic_error"
dotnet_version: "9.0"
resolution_type: "code_fix"
severity: "medium"
tags: [dashboard, ef-core, expression-trees, sargable, indexes, authz, home-feed, drilldown, react, url-state, code-review]
---

# Dashboard counts that drill to the same rows: shared EF predicates and scope-safe home feeds

## Problem
Plan 5 added role homes (`GET /api/home`) and a district compliance board whose every number must
"drill to the roster with the matching filter" — the rows a user lands on have to equal the tile they
clicked. It also surfaced "drafts in progress" on the staff home. The first cut passed its tests and
the live smoke, and review still found four classes of defect worth keeping.

## What went wrong

1. **Two definitions of one number.** The board computed `Due30`/`Due60` from a caller-supplied
   `from`/`to`, while the roster's `attention=Due30` was anchored on today. With the default range the
   counts matched; with any other range they silently diverged. The district-admin home re-derived the
   same summary from a different data path (`ObligationService` list → in-memory buckets).
2. **"I last edited it" as an authorization predicate.** Drafts were selected by
   `LastEditedByUserId == caller OR active team member`. Removing someone from the team (or transferring
   the student) revoked their access to the document but not its row on their home page.
3. **Indexes the optimizer cannot reach.** `SchoolStudents(AnnualReviewDueDate)` was added for the
   board, but the predicate was `(AnnualReviewDueDate ?? IepDate + 365) < today` — a COALESCE over the
   column, so SQL Server scans.
4. **Unbounded lists on a summary page.** `overdueByCaseManager` had no cap while every sibling list
   did; a mid-size district could push it past the client table's 500-row ceiling.

## Fixes

- **One predicate, many consumers.** `StudentAttentionRules` exposes `Expression<Func<SchoolStudent,bool>>`
  factories (`OverdueAnnual`, `OverdueReeval`, `DueWithin(from,to)`, `UnknownDates`, `NoLead`, `NoFamily`)
  used verbatim by the roster `attention` filter (`.Where`) and the board (`.Count(predicate)` per school).
  `Due30`/`Due60` are always today-anchored; a chosen range becomes its own `dueInRange` bucket whose
  drill link carries `attention=DueInRange&from=&to=`, and the roster accepts those params. A parity test
  asserts `board count == roster total` for every key on a non-default range. The admin home's summary
  is the board's own result (`GetComplianceBoardAsync` with no filters) and shares the DTO mapper.
- **Current access only.** Home drafts require an active `StudentTeamMember` row, an active
  `SchoolStudentAccess` row, or admin scope (active schools only); `LastEditedByUserId` is only an
  ordering signal. Tests: removed member and transferred student no longer see the draft.
- **Sargable branches.** `(AnnualReviewDueDate != null && AnnualReviewDueDate < today) ||
  (AnnualReviewDueDate == null && IepDate != null && IepDate < today.AddDays(-365))` — the same set as
  the COALESCE form, but the populated-column branch seeks the index. `AccessAuditLogs(ActorUserId,
  CreatedAt)` added for the adoption "active in window" EXISTS.
- **Cap + total.** `overdueByCaseManager` returns 50 rows (sorted by student, never by staff) plus
  `overdueByCaseManagerTotal`; the UI links to the board for the rest.
- Web: board filters live in the URL (`?school=&range=`), adoption/engagement load in parallel with
  the board and fail per piece, tab titles never carry a student's name, error states always render a
  retry notice (never an empty section), `HomeDto` is a real discriminated union.

## Verification
- `dotnet test` 736 (was 724); `vitest` 385 (was 344). Live on QA: board `from=2026-10-01&to=2026-10-31`
  → `dueInRange: 1`; roster `attention=DueInRange&from…&to…` → the same one student; home
  `complianceSummary` equals the board summary; admin home measured at 7 queries.
- Review pass 1 (8 specialists, Sonnet): 0 P1 / 13 P2 / 17 P3 → fixed in `5a0541a`; pass 2 on the
  combined diff: 0 P1 / 0 P2 / 17 P3 (`todos/086–087`).

## Prevention
- When a count links to a list, build both from the same `Expression` and write the parity test
  first — for the non-default inputs, not only the defaults.
- Never derive visibility from an audit column (`LastEditedBy`, `CreatedBy`); derive it from the
  same access rows the read path checks.
- Adding an index is not done until the predicate is written so the optimizer can use it; avoid
  `??`/COALESCE/CASE around indexed columns in EF predicates — split into OR branches.
- Every list on a summary surface gets a cap and a "view all" link.

## Related
- Plan: `docs/plans/2026-09-15-005-feat-role-homes-operational-dashboards-plan.md`
- `docs/solutions/performance-issues/2026-09-15-set-based-xlsx-import-commit-…` (per-request authz memo the home relies on)
- P3 follow-ups: `todos/086-*`, `todos/087-*`
