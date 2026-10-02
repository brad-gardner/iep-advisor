---
module: "AnalysisRun"
date: "2026-10-02"
problem_type: logic_error
component: service_object
symptoms:
  - "A document's analysis showed Completed on the IEP page but Error on the child Analysis timeline (QA run 15 vs IepAnalysis 4)"
  - "Goal (SMART) ratings copied from legacy analyses never rendered on the child timeline"
  - "Two unlinked analyses of the same IEP existed in two places (QA runs 15 and 16)"
  - "Review found a stale-run sweep could fail and refund a run that was still executing, after which the run completed anyway"
  - "Document pages polled a dead run for 15 minutes after it failed"
root_cause: logic_error
resolution_type: code_fix
severity: high
dotnet_version: "9.0"
tags: [analysis-run, background-job, race-condition, conditional-update, ef-core, cartesian-query, backfill, polling, react-hooks, usage-metering]
---

# Troubleshooting: Two analysis engines drift; one engine needs race-safe terminal states and a backfill that converges

## Problem

IEP Advisor had two analysis engines. One was the legacy per-document `IepAnalysis`/`EtrAnalysis`, which the IEP and ETR pages ran. The other was `AnalysisRun`, which the child page ran. Legacy results reached the child timeline only through a boot-time, one-way backfill that skipped rows it had already copied, so the two views disagreed. Branch `refactor/unified-analysis-one-engine` replaced both with one engine, `AnalysisRun`. Document pages became views of the latest run that includes the document. Each run now makes one Claude call per document plus one synthesis call. Three review passes then found five classes of defect in the new engine, described below.

## Environment
- Module: `AnalysisRunService`, `AnalysisRunBackfillService`, `AnalysisRunWorker` (api), `useSourceAnalysis` / `usePolling` / `useAnalysisRun(s)` (web)
- .NET 9, EF Core 9.0.1, SQL Server (tests on SQLite in-memory); React 19 + Vite + Vitest
- Date: 2026-10-02

## Symptoms
- QA run 15 (the backfilled copy of `IepAnalysis:4`) stayed **Error**. The legacy row had been re-analyzed in place to **Completed**, but `AnalysisRunBackfillService` only checked whether a `BackfillSourceKey` existed, so it never updated the copy.
- Backfilled goal ratings were stored as one `annual_goals` section holding a JSON **array**. That shape cannot deserialize into `AnalysisRunSectionResult`, so `MapSectionToModel` returned null and the UI dropped the section.
- (Review) With one Claude call per source, a multi-document run could pass the 30-minute stale threshold while still working. The sweep, running in its own DbContext, released the usage unit and set Error. The executor's tracked `Status = Completed` save then overwrote it. The result was a Completed run with no usage unit charged.

## What Didn't Work

**Heartbeat-free stale detection.** `UpdatedAt` was stamped once, on entering Running. A legitimately long run, with N sources and calls of up to 15 minutes each, looked exactly like a hung one.

**Tracked-entity terminal writes.** Even with an accurate staleness rule, two writers with no concurrency token both "won". The executor's tracked save wrote only the columns it had changed, so it silently replaced the sweep's Error.

**Two-pass backfill convergence.** The first version wrote legacy shapes on create and converted them "on the next boot". Every legacy row written since the last deploy rendered degraded until a restart. A mid-batch rebuild also called `ChangeTracker.Clear()` before the batch's restored `CreatedAt` values were saved, so they were silently lost.

## Solution

**1. Heartbeat plus conditional terminal transitions.** Before each source call and before the synthesis call, the executor heartbeats with a conditional update and stops if it no longer owns the run:

```csharp
var heartbeatRows = await _context.AnalysisRuns
    .Where(r => r.Id == run.Id && r.Status == AnalysisRunStatus.Running)
    .ExecuteUpdateAsync(s => s.SetProperty(r => r.UpdatedAt, DateTime.UtcNow), ct);
if (heartbeatRows == 0) return; // the sweep already failed (and refunded) this run
```

`FailRunAsync` and the executor's Completed write are both `ExecuteUpdateAsync … WHERE Status IN (Running, Pending)`. Whichever writer affects 0 rows lost the race and does nothing. The refund (`ReleaseUsageByIdAsync`) runs only after this writer has won, inside the same transaction, on the same scoped DbContext. The sweep passes its cutoff into the conditional update (`UpdatedAt <= cutoff`), so a run that heartbeated after the sweep's SELECT survives. A failed run also moves any unfinished sources to Error, so document pages stop polling.

**2. Don't project two sibling collections in one EF query.** `GetRunAsync` first projected `Sources` and `Sections` in a single `Select`. EF Core 9 still emits that as **one** joined query: sources × sections rows, repeating every run-level text column. A projection does not split automatically, and the app has no `QuerySplittingBehavior`. The fix projects run plus `Sources` metadata (never `SourceContentSnapshot`) and loads `Sections` in a separate query after the access check. `GetLatestForSourceAsync`, which document pages poll every 5 s, loads only the requested source's sections. A new `(SourceType, SourceId)` index (migration `AddAnalysisRunSourceLookupIndex`) covers the "latest run including document X" lookups.

**3. Backfill writes the final shape, with the right date, in one transaction.** Create and rebuild share the same converters (`iep_goals` object, `etr_completeness`/`etr_eligibility`, red/yellow red flags), so a newly created run is already correct. The run's `CreatedAt` comes from the legacy row's **`UpdatedAt`**, because legacy engines re-analyzed in place; using `CreatedAt` made re-analyzed documents look stale. Pending creates are flushed in a transaction before any rebuild. Legacy section kinds go through the same `AnalysisRunSectionKinds.Sanitize` as live output.

**4. Billing edges.**
- Usage refunds: a run with any `InvalidResponse` (unparseable output) failure keeps its usage unit, so a crafted document cannot get free retries (the original todos/P2-02 rationale). A run is refunded only when every failure is a provider or transient failure.
- Source cap: at most 5 sources per run (`[MaxLength(5)]` plus a service check), so one unit cannot buy unbounded calls.

**5. Web polling and races.**
- `usePolling` gets a `cancelled` flag, checked before and after the awaited call, so no orphan loop survives unmount.
- Each hook bumps a request-generation ref on id change, load and trigger, and drops stale results.
- `RunDetail` is keyed by run id.
- In `useSourceAnalysis`, only **HTTP 404** means "never analyzed". Any other load error keeps the run and offers "Try again", not "Analyze". A created run is seeded from the create response, so a failed follow-up load cannot offer a second paid run.

## Verification

- `dotnet build` clean. `dotnet test IepAssistant.Services.Tests`: **1306 passed**. Includes tests for both race interleavings, sweep-wins-mid-run stops further calls, cutoff re-check, refund rules, single-pass backfill shapes and dates, a mixed-batch flush, and section-kind sanitization.
- Web: `npx tsc --noEmit` clean, `npm run test:types` (the CI gate) exit 0, `npx vitest run` **761 passed**. Includes polling-cancel, latest-wins, 404-only and trigger-seeding tests. impeccable detector found nothing on the changed UI files.
- **Limitations:**
  - The tests run on SQLite with one shared connection, so they prove which writer wins but not SQL Server isolation under real concurrency.
  - The rewritten e2e spec (`e2e/tests/iep-analysis.spec.ts`) needs a running app and real Claude calls and was not run.
  - Three P2s were left open at the review cap (todos/239):
    - the Pending→Running entry transition is still an unconditional tracked save, so another instance's reconcile could be undone;
    - a null item inside a model-returned list throws outside the per-source catch and refunds the whole run;
    - the Bruno list example is out of date.

## Why This Works

1. **Root cause.** Ownership of a run's lifecycle was never made explicit. Several writers (executor, sweep, startup reconcile) mutated status through tracked entities, and staleness was measured from a timestamp nothing refreshed. On the data side, copying was treated as a one-time event instead of convergence toward a canonical shape.
2. **Why the fix holds.**
   - Conditional, single-statement updates make each transition atomic: exactly one writer can move a run out of Running/Pending, and only that writer may refund.
   - The heartbeat makes "stale" mean "no progress for 30 minutes" rather than "long".
   - The backfill is idempotent and lands rows in their final shape, so a re-run fixes earlier mistakes instead of keeping them.
3. **Underlying API traps.**
   - EF's change tracker writes only modified columns, without consulting the database.
   - EF Core does not split sibling-collection projections into separate queries.
   - The client treated every load error as "not found".

## Prevention

- Any background job that can fail or refund a unit of work must use conditional updates keyed on the expected current status, and must heartbeat progress before long external calls. Grep for tracked `Status =` assignments followed by `SaveChangesAsync` in workers.
- Use `.AsSplitQuery()` or a separate query whenever a projection includes more than one collection navigation. Never select large text columns on polled endpoints.
- Backfills must write final shapes in one pass and be safe to re-run. Date copied rows from the source row's last-modified time when the source mutates in place.
- In fetch hooks, only a definitive 404 may clear state to "never existed". Polling loops need cancellation and latest-wins guards.

## Related

- Plan: `docs/plans/2026-10-02-001-refactor-unified-analysis-one-engine-plan.md`; design: `docs/designs/2026-10-02-unified-analysis-design.md`; brainstorm: `docs/brainstorms/2026-10-02-unified-analysis-navigation-brainstorm.md`
- `docs/solutions/logic-errors/2026-09-16-family-draft-sharing-untrusted-ids-in-prompts-and-hidden-editor-reveal.md` (model-returned ids are validated, as goal ids are here)
- `docs/solutions/best-practices/2026-09-19-tool-using-advocate-over-sse-what-a-streaming-agent-in-aspnet-has-to-get-right.md` (reserve before calling, refunds outside the change tracker)
- Open follow-ups: `todos/239-ready-p2-analysis-run-outstanding-at-review-cap.md`, `todos/237`, `238`, `240` (P3)
