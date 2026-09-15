---
title: "feat: Student roster lifecycle, safe repeatable XLSX import, and IEP teams with functional roles"
type: feat
status: active
date: 2026-09-15
origin: docs/gap/combined-findings.md
slicing_approach: vertical
series: school-sale-readiness 3/8
---

# feat: Roster, XLSX import, IEP teams (combined findings C05, C06)

## Problem Statement

`SchoolStudent` can only be created (`POST /api/educator/students`) with school, first/last name, free-text grade and free-text disability. No edit, archive, exit or transfer; no stable district student ID; DOB exists on the entity but not the form. There is no import. Org roles are `DistrictAdmin/SchoolAdmin/Teacher`; the per-student "team" is an ACL (`SchoolStudentAccess.Role: Viewer|Collaborator|Owner`) with no notion of case manager, SLP, gen-ed teacher or LEA rep, and `OrgAccessService` restricts non-admin staff to one school so a cross-building provider cannot be assigned.

## Decisions (defaults)

1. **Student identity key = `(DistrictId, ExternalStudentId)`**, string, leading zeros preserved, unique filtered index where not null. Import matches only on it; never on name.
2. **Controlled values:** `GradeLevel` enum (PK, K, 1–12, plus `Ungraded`), `DisabilityCategory` enum (IDEA 13 + Ohio-specific labels as display names), `HomeLanguage` free ISO-ish text (default `en`). Existing free-text values are migrated by best-effort mapping; unmapped go to `Other` with the original kept in a new `LegacyDisabilityText` column (dropped in a later cleanup).
3. **Lifecycle:** `Status` enum `Active|Exited|Archived`, `ExitedAt`, `ExitReason` (enum: Graduated|Transferred|Withdrawn|Declassified|Other), `TransferSchool(newSchoolId)` keeps documents, links, team (team rows for staff not at the new school are deactivated with a note).
4. **Team model:** new `StudentTeamMember { SchoolStudentId, UserId, TeamRole enum (CaseManager|InterventionSpecialist|GeneralEducationTeacher|SpeechLanguagePathologist|OccupationalTherapist|PhysicalTherapist|SchoolPsychologist|Counselor|LeaRepresentative|Interpreter|Other), IsLead, IsActive }`. Exactly one active `IsLead` (the case manager of record) → also `SchoolStudent.CaseManagerUserId`. **Permission stays separate:** adding a team member creates/updates a `SchoolStudentAccess` with a default (`CaseManager`→Owner, providers/gen-ed→Collaborator, LEA/Interpreter→Viewer) that an admin can override. Removing a member deactivates both.
5. **Org roles:** extend seed with `RelatedServiceProvider` (id 4) and `GeneralEducator` (id 5) — same authz tier as Teacher, but `RelatedServiceProvider` may be granted access to students in any active school of the district (multi-building). `StaffProfile.SchoolId` stays the "home" school.
6. **Import format:** single-sheet `Students` XLSX, columns: `StudentId*`, `SchoolName*` (or `SchoolCode`), `FirstName*`, `LastName*`, `DateOfBirth*` (date), `Grade*`, `DisabilityCategory`, `HomeLanguage`, `CaseManagerEmail`, `IepDate`, `AnnualReviewDue`, `EtrDate`, `ReevaluationDue`, `Status`. Second sheet `Values` lists allowed grades/disabilities/schools. Template is generated server-side so the school list is live. Library: **ClosedXML** (MIT). Formulas/macros never evaluated (values read as cell cached values; `.xlsm` rejected).
7. **Import semantics:** validate all rows → preview `{ new, updated, unchanged, error }` with a downloadable error report → commit only valid rows (`commitValid=true`) or none. Blank cells never clear a value; a literal `CLEAR` token clears. Missing rows never exit anyone. Every commit writes an `ImportBatch` + `ImportRow` outcomes. Staff import is the same pipeline with a `Staff` sheet (creates invites for unknown emails).
8. **Timeline columns** (`IepDate` etc.) are stored now on `SchoolStudent` so plan 4 can alert on them.

## Proposed Solution

### Backend
- Migration: `SchoolStudent` + `ExternalStudentId`, `GradeLevel` (enum string), `DisabilityCategory` (enum string), `LegacyDisabilityText`, `HomeLanguage`, `Status`, `ExitedAt`, `ExitReason`, `CaseManagerUserId`, `IepDate`, `AnnualReviewDueDate`, `EtrDate`, `ReevaluationDueDate`; `StudentTeamMember`; `ImportBatch`/`ImportRow`; `OrgRole` seeds 4–5.
- `EducatorService`: `UpdateStudent`, `ExitStudent`, `ArchiveStudent`, `TransferStudent`, `SearchStudents(query, schoolId, status, page)`; team CRUD `Get/Add/Update/RemoveTeamMember`, `SetLead`; bulk `AssignCaseManager(studentIds[], userId)`.
- `RosterImportService`: `GenerateTemplateAsync(districtId)` → xlsx bytes; `PreviewAsync(districtId, stream)` → `ImportPreviewModel`; `CommitAsync(batchId)`. `StaffImportService` sharing the reader. Size cap 5 MB, 5,000 rows.
- Endpoints under `/api/educator/students/{id}` (PUT, POST exit/archive/transfer), `/api/educator/students/{id}/team` (GET/POST/PUT/DELETE, POST lead), `/api/educator/students/bulk/case-manager`, `/api/district/imports/students/template`, `/api/district/imports/students/preview`, `/api/district/imports/{batchId}/commit`, `/api/district/imports` (history), `/api/district/imports/{batchId}/errors.xlsx`.
- `OrgAccessService.CanActOnStudentAsync`: `RelatedServiceProvider` passes the school check for any active school in the district (still needs an access row).

### Web
- Roster: search box, school/status/grade filters, pagination, "Import" button (DistrictAdmin/SchoolAdmin), bulk-select → "Assign case manager".
- Student detail: **Edit** drawer (all fields with enums), Exit/Archive/Transfer actions in a menu with ConfirmDialog; **IEP team panel** replaces "Assigned staff": lead case manager row, member rows with team role + effective permission badge, "Add member" search-by-name/email with role select, parent links moved into the same panel as "Family".
- Import wizard (`/educator/admin/imports`): Download template → Upload → Preview table (counts + per-row status, error download) → Commit → Result; history list.
- Staff page: "Import staff" using the same wizard with the Staff sheet; role select includes the two new roles.

## Implementation Phases

### Phase 1 — Student record + lifecycle + roster UX (vertical)
- Migration, enums, mapping of legacy free text, update/exit/archive/transfer, search/pagination, edit drawer, roster filters. Tests for authz on each mutation, transfer side-effects, legacy mapping.
- Checkpoint: edit a student's DOB and grade; exit and reactivate; transfer keeps documents.

### Phase 2 — Team model + roles
- `StudentTeamMember`, seeds, lead rule, permission defaults, provider cross-school, bulk assign; team panel UI; team in evidence bundle (plan 2's `TeamMember` items read this table).
- Tests: single lead invariant; provider in another building gets access; removing lead requires replacement; permissions derive/override.
- Checkpoint: build a 5-person team from the student page; sign in as the SLP and see only that student's scoped view.

### Phase 3 — XLSX import (students, then staff)
- ClosedXML, template generation, preview/commit, batch history, error workbook; wizard UI; staff sheet.
- Tests: identical re-upload → 0 changes; leading-zero IDs; unknown school → row error; duplicate IDs in file → both rows error; blank cell preserves; `CLEAR` clears; `.xlsm` rejected; 5,001 rows rejected.
- Checkpoint: import 40 seeded students, re-upload unchanged, change one, transfer one — no duplicates.

## Acceptance Criteria

- [ ] Student has external ID, DOB, enum grade/disability, language, lifecycle status, timeline dates, case manager; all editable with authz; exit/archive/transfer preserve history.
- [ ] Roster supports search, filters, pagination, bulk case-manager assignment.
- [ ] IEP team panel: one lead, typed roles, add/remove, permission badge distinct from role; provider role can span buildings.
- [ ] XLSX template downloadable with live allowed values; preview shows new/updated/unchanged/error; commit is idempotent; batch history and error report available; formulas/macros never executed.
- [ ] Staff import creates invites for new emails and updates role/school for existing staff.
- [ ] All checks pass.

## System-Wide Impact

- `DistrictDashboardModel.StudentsWithoutStaff` → becomes "students without a lead case manager" (keep DTO name, change semantics + label).
- `EducatorService.GetStudentsAsync` callers (dashboard attention, roster) must apply status filter (default Active).
- Legacy `SchoolStudentAccess` UI (`staff-access/*`) is replaced by the team panel; keep API for compatibility one release.

## Dependencies & Risks

- Data migration of free-text grade/disability on QA data — mapping is best effort; unmapped preserved.
- ClosedXML adds ~2 MB; acceptable.

## Sources

- Origin: [combined-findings](../gap/combined-findings.md) C05, C06; J2 school onboarding; P4/P5/P6 personas
- Code: `EducatorService.cs`, `OrgAccessService.cs`, `OrgRoleConfiguration.cs`, `SchoolStudent.cs`, `web/src/features/educator/*`, `district-admin/*`, `staff-invites/*`
