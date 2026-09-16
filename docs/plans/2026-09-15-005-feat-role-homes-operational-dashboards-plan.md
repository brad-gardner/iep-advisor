---
title: "feat: Role-specific operational homes and district compliance dashboard with analytics"
type: feat
status: completed
date: 2026-09-15
origin: docs/gap/combined-findings.md
slicing_approach: vertical
series: school-sale-readiness 5/8
depends_on: docs/plans/2026-09-15-004-feat-meetings-deadlines-notifications-calendar-plan.md
---

# feat: Operational homes and dashboards (combined findings C08)

## Problem Statement

`EducatorDashboard` renders setup tiles (schools/staff/invites/attention) for admins and a single "Go to your students" card for everyone else. The parent `DashboardPage` is a child list, an account box and quick actions. None answers "what needs me now". With plans 3–4 in place there are meetings, obligations, drafts, shares and notifications to render.

## Decisions (defaults)

1. **One `HomeService` per role returning a typed `HomeModel`** (server-computed, single request, scoped by `OrgAccessService`), rather than many client fetches.
2. **Case manager / provider / gen-ed home** (`Teacher`, `RelatedServiceProvider`, `GeneralEducator`): sections — *This week* (meetings I'm on), *Due soon / overdue* (obligations where I'm lead), *Drafts in progress* (instances I edited or am on the team for, with completeness %), *Shared drafts awaiting family* / *Family responses to review* (plan 6 fills; empty-safe now), *Provider requests I owe* (plan 7 fills). Provider variant orders "what I owe" first.
3. **School admin / LEA home** (`SchoolAdmin`): *Meetings this week in my building* with "brief" link (plan 7 supplies the brief; link disabled until then), *Overdue/at-risk by case manager* (counts, never ranking language — table sorted by student, not by staff), *Unsigned finalized documents* (plan 7), *Roster attention* (no lead case manager, no family link) — keep existing tiles below.
4. **District admin home**: **Compliance board** — overdue annual reviews, overdue re-evaluations, due in 30/60 days, students with Unknown dates, students without lead, by school; **Adoption** — staff active in last 14 days by school, drafts started / finalized in last 30 days; **Family engagement** — students with linked family, drafts shared, responses received (plan 6), framed as evidence. Filters: school, date range. Every number drills to the roster with the matching filter (`/educator/students?attention=...` extended).
5. **Parent home**: meeting-relative — *Next meeting* card with countdown and RSVP; *Documents to review* (shared drafts from plan 6; finalized versions now); *Your children* (existing); *Recent progress reports*; setup notices kept but demoted.
6. **Charts:** small inline SVG components (`components/ui/charts/{bar,stack,sparkline}.tsx`) using the brand palette — no chart library. Every tile: label, value, denominator/date range, drilldown link.
7. **Polish in scope:** sidebar footer user-name clipping; duplicated H1/H2 district name on educator home; tab title per route (`document.title` via a `usePageTitle` hook); loading failures render an error notice, never an empty state.

## Proposed Solution

### Backend
- `HomeController`: `GET /api/home` → dispatches on staff org role / parent / student to `HomeService.GetStaffHomeAsync`, `GetParentHomeAsync`, `GetStudentHomeAsync` (student: next meeting + workspace nudge).
- `DistrictService.GetComplianceBoardAsync(scope, schoolId?, range)` → `ComplianceBoardModel` (counts + per-school rows + drill filters); `GetAdoptionAsync`; `GetEngagementAsync`.
- Roster attention filters extended: `overdue-annual`, `overdue-reeval`, `due-30`, `unknown-dates`, `no-lead`, `no-family`.
- Draft completeness % computed from required fields + semantic checks (reuse plan 1 client rules server-side in `DocumentCompletenessService`).

### Web
- `features/home/`: `StaffHomePage` (variants by org role), `ParentHomePage` (replaces `DashboardPage` body), `StudentHomePage` additions; shared `HomeSection`, `WorkItemRow`, `StatTile`, chart primitives.
- `features/district-admin/pages/compliance-board-page.tsx` at `/educator/admin/compliance` (also the district admin home's top block), filters, drilldown links.
- Sidebar: "Calendar", "Notifications", "Compliance" (admins) entries; footer fix; `usePageTitle`.

## Implementation Phases

### Phase 1 — Staff home (case manager / provider / gen-ed) + polish
- `HomeService` staff model, page, sections with empty states, completeness service, sidebar/footer/title fixes. Tests: scoping (only my meetings/obligations), completeness math, error-state rendering.
- Checkpoint: seeded case manager logs in and sees this week's meeting and a due-soon review; clicking opens the student.

### Phase 2 — School admin + district compliance board + adoption/engagement
- Board endpoints, page, drilldowns, charts. Tests: counts by school; SchoolAdmin sees only own school; drill filter parity with counts.
- Checkpoint: district admin sees overdue count, clicks, lands on the filtered roster with the same rows.

### Phase 3 — Parent home
- Parent home model + page; next meeting/RSVP; documents to review; demote setup notices. Tests: parent with two children; parent with no school link sees Mode C home unchanged.
- Checkpoint: linked parent sees "Annual review in 12 days" and the finalized IEP link.

## Acceptance Criteria

- [x] Each role's home answers "what needs me" from live data; every alert/tile links to the actual student/document/meeting; completing the task removes the item on reload. *(Live: Steph's home shows "This week", the Due-soon annual review and four drafts with completeness %.)*
- [x] District compliance board with school/date filters, drilldowns whose row counts match the tiles, adoption and engagement tiles; SchoolAdmin scoped to own school; no staff ranking or family risk scoring anywhere. *(Live: tiles 0/0/1/1/4/3; "No case manager" → `/educator/students?attention=NoCaseManager&school=37` lists the same 3 students; roster totals equal board counts, also asserted by a parity test.)*
- [x] Parent home is meeting-relative and shows finalized documents and upcoming meetings.
- [x] Unknown dates surface as Unknown, never as healthy; load failures show an error notice with retry.
- [x] Sidebar footer no longer clips; no duplicate district heading; `document.title` set per route. *(Live: "Maple Ridge Middle School · IEP Advisor".)*
- [x] All checks pass: `dotnet test` 724, vitest 344, tsc + test:types, build, guard:ux (its recursive walk already covers the new folders); lint 36 (baseline).

## Implementation notes (2026-09-16)

- Migration `AddHomeDashboardIndexes` applied to QA.
- `StudentAttentionRules` holds the EF predicates shared by the roster `attention` filter and the compliance board, so counts and drilldowns cannot drift; `Due60` is cumulative (includes `Due30`).
- Adoption "active" = at least one access-audit entry in the window (the audit table exists); RSVP/notification reads are not audited yet.
- The roster school filter param is `school=` (not `schoolId=`); drill links use it.
- Plan-6/7 sections (shared drafts, family responses, provider requests, unsigned finalized) render empty-safe hints.

## Operational validation notes (for ship)

- **Runtime impact:** `GET /api/home` (≤ 7 queries per role, measured) replaces the dashboard aggregate for staff homes; three new district endpoints. Watch: home latency for large districts (drafts capped at 20; board counts are index-backed via the new indexes).
- **Healthy signal:** each role lands on a populated home; board tiles equal the roster totals for the same filter.
- **Failure/mitigation:** home load failures render a retry notice (no blank dashboard); the previous educator dashboard component was removed — revert the web commit to restore it if needed. Owner: Brad; window: first admin session after deploy.

## System-Wide Impact

- `DashboardPage` (parent) and `EducatorDashboard` are replaced; e2e `district-dashboard.spec` and `smoke.spec` need updating.
- Home endpoint aggregates several queries — keep under ~6 round trips; add indexes on `Meeting(StartsAtUtc)`, `SchoolStudent(AnnualReviewDueDate)`, `(ReevaluationDueDate)`.

## Dependencies & Risks

- Depends on plans 3–4 data. Plan 6/7 sections are designed empty-safe and light up later.

## Sources

- Origin: [combined-findings](../gap/combined-findings.md) C08; personas P4–P7 "Design implications"; claude-findings Goal 3 walkthrough notes
- Code: `educator-dashboard.tsx`, `district-dashboard-tiles.tsx`, `dashboard-page.tsx`, `sidebar.tsx`, `DistrictService.GetDashboardAsync`
