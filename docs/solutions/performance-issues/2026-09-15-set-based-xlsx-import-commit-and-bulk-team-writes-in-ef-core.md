---
module: "Roster"
date: "2026-09-15"
problem_type: "performance_issue"
component: "service_object"
symptoms:
  - "Roster import commit did one SaveChanges plus 4-6 round trips per row: a 5,000-row file meant ~30,000 statements inside one transaction"
  - "Bulk 'Assign case manager' ran 3 authorization queries and 2 SaveChanges per selected student"
  - "The dashboard 'No case manager' deep link fetched 500 roster rows and filtered client-side, but the server clamps pageSize to 200 - students silently disappeared"
  - "Double-clicking Commit could run the import twice; the second run's rows failed as duplicates"
root_cause: "logic_error"
dotnet_version: "9.0"
resolution_type: "code_fix"
severity: "high"
tags: [ef-core, batching, savechanges, change-tracker, xlsx-import, closedxml, filtered-unique-index, authz, pagination, code-review]
---

# Set-based XLSX import commit and bulk team writes in EF Core

## Problem
Plan 3 (roster lifecycle, IEP teams, XLSX import) shipped a per-row commit loop: each imported
student was inserted with its own `SaveChangesAsync`, then the shared `StudentTeamWriter` upserted the
case-manager team row (member lookup, access lookup, save, lead lookup, save). With the contract's
5,000-row cap that is on the order of 30,000 sequential statements, all under one transaction holding
insert locks, while `DetectChanges` walked a tracker that grew past 20,000 entities. Independent review
rated it P1 alongside a client-side "attention" filter that requested 500 rows the server clamps to 200.

## Root cause
The row-at-a-time writer was reused verbatim for batch work. Correct for one student, quadratic for a
file. Authorization was likewise looped per id even though the same scoped query that powers the roster
already encodes the rule.

## Solution

**Pre-load, mutate in memory, save per chunk.** `RosterImportService.CommitAsync` now:
1. loads `ImportRows` `AsNoTracking`, evaluates every row first;
2. processes New/Updated rows in chunks of 500: one query for the chunk's tracked existing students,
   two for their `StudentTeamMember` / `SchoolStudentAccess` rows, then in-memory mutation through the new
   `StudentTeamBatch` (lead swaps, access defaults, deactivation of non-portable members when the school
   changes);
3. flushes once per chunk (twice when a student still had a different active lead: **demotes land in the
   first save, promotions in the second**, so the filtered unique index
   `IX_StudentTeamMembers_SchoolStudentId_ActiveLead` is never tripped regardless of statement order);
4. `ChangeTracker.Clear()` between chunks; row outcomes are written by attaching minimal entities and
   marking only the changed properties;
5. a chunk-level `catch (DbUpdateException) when (IsExternalIdCollision)` clears the tracker (EF's
   savepoint rolls the chunk back) and re-applies that chunk row-by-row so a duplicate lands on its own
   row; any other `DbUpdateException` propagates and the whole transaction rolls back.

Measured by the new test with an EF command interceptor: a 1,000-row commit with case managers is
**3 SaveChanges / 9 SELECTs** (was ~3,000 / ~6,000). SQLite does not batch DML, so the assertion bounds
saves and queries rather than raw commands.

**Commit-once.** `ImportBatchClaim` moves the batch `Previewed → Committing` with a single
`ExecuteUpdateAsync … WHERE Status = 'Previewed'` and refuses when 0 rows were affected; failure releases
it back to `Previewed`. No concurrency token, no migration (string column).

**Bulk assign.** One scoped query (`ScopedStudents(ctx).Where(id ∈ ids)`) authorizes all ids at once —
a count mismatch is a permission failure, which also covers unknown ids — followed by the same
pre-load/in-memory/1–2-save pattern and a 500-id cap.

**Attention filter server-side.** `GET /api/educator/students?attention=NoCaseManager|NoLinkedParent`
composes the dashboard predicates onto the role-scoped query, so paging works and nothing is clamped
away; the client's 500-row fetch, `useAttentionIds` and the dead `getStudents()` wrapper were deleted.

**Audit after commit.** Per-student `AuditAction.Edit` entries are enqueued only after
`tx.CommitAsync()` — the audit channel is fire-and-forget, so recording inside the loop would leave
phantom edits after a rollback.

## Verification
- `dotnet test` 577 (was 567): `Commit_1000NewRowsWithCaseManagers_IsSetBased_AndAuditsAfterCommit`,
  `Commit_LeadSwapAcrossManyStudents_RespectsSingleActiveLeadIndex`,
  `Commit_ClaimsTheBatch_SoAnInFlightCommitIsRefused_AndAFailedCommitReleasesIt` (cancels mid-insert and
  asserts the batch returns to `Previewed` with no partial rows),
  `AssignCaseManagerBulk_IsSetBased_ForManyStudents_AndCapsTheSelection`,
  `SearchStudents_AttentionFilters_NarrowServerSide_AndPage`.
- Live on QA: `?attention=NoCaseManager` → 3 of 4 students, paged; a 501-id bulk request → 400;
  second commit of a batch → "This import has already been committed."
- Independent review: pass 1 (8 specialists) 3 P1 / 16 P2 / 46 P3 → all fixed; pass 2/3 on the
  combined diff: 0 P1 / 0 P2 / 16 P3 (follow-ups in `todos/042–043`).

## Prevention
- Any writer that will be called from a loop needs a batch entry point that takes the pre-loaded
  state; keep the single-row API as a thin wrapper over it.
- When a unique/filtered index enforces an invariant that a batch can transiently violate, order the
  saves (demote → promote) rather than relying on EF's statement order.
- Enqueue fire-and-forget audit events after the transaction commits.
- If the UI has to narrow a list by a rule the server already knows, add a server filter; never
  over-fetch and filter client-side (server clamps and pagination will bite).

## Related
- Plan: `docs/plans/2026-09-15-003-feat-student-roster-xlsx-import-iep-teams-plan.md`
- `docs/solutions/best-practices/2026-09-15-grounding-ai-suggestions-…` (per-request authz memo in
  `OrgAccessService` — the per-id authz loop here is the case that memo could not help)
- P3 follow-ups: `todos/042-*`, `todos/043-*`
