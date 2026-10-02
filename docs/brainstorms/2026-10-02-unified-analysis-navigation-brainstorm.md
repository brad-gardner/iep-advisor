# Unified Analysis — One Engine, Two Views — Brainstorm

**Date:** 2026-10-02
**Status:** Draft
**Precedes:** `/sht:plan`
**Builds on:** `docs/brainstorms/2026-05-26-school-side-and-analysis-rework-brainstorm.md` (introduced `AnalysisRun`), plan `docs/plans/2026-05-28-001-feat-school-side-and-analysis-rework-plan.md`, the Claude output-budget and keepalive fixes of 2026-09-29 (`3510b98`, `d0f6e8f`, `2a13516`).

## Context

A parent sees "Analysis" in two places that do not know about each other:

- **IEP / ETR page → Analysis tab.** Runs the legacy per-document engine (`IepAnalysis` / `EtrAnalysis`): section notes with suggested questions, per-goal SMART ratings, red flags, advocacy-gap analysis.
- **Child page → Analysis tab.** Runs `AnalysisRun`: pick any IEPs / ETRs / progress reports, get per-source sections plus a cross-document synthesis (timeline, contradictions, progression), red flags, advocacy gaps — but **no per-goal SMART ratings**.

The May rework meant `AnalysisRun` to *replace* the per-document engine ("existing rows can remain as legacy", "users see all history in one timeline"). The replacement never happened on the document pages. Legacy analyses reach the child timeline only through `AnalysisRunBackfillService` at startup, which never revisits a row it already copied.

Observed on QA, child 1 (Jacob), IEP document 35:

- Run 15 is the backfilled copy of `IepAnalysis:4`, taken while that analysis was mid-failure. The IEP page now shows it **completed**; the child timeline still shows it as **Error**.
- Run 16 is a separate child-level run over the same IEP. One document, two analyses, two places, no link between them.

Other features read the legacy tables directly: meeting prep (Mode A reads `IepAnalyses`), the advocate toolset, IEP comparison, account purge/export.

## What We're Building

**One analysis engine (`AnalysisRun`), seen from two places.**

1. **Child page → Analysis** is the home of analysis: the full run history for the child, and where multi-document runs are started (IEP + ETR, year over year).
2. **IEP page and ETR page → Analysis** become a *view* onto runs:
   - Show this document's slice of the **latest run that included it**: its sections, its goal ratings (IEPs), its red flags.
   - If that run also covered other documents, say so ("Part of an analysis with the 2025 ETR") and link to the full run on the child page.
   - "Analyze this IEP" / "Re-analyze" starts a **single-source run** — the same engine, pre-selected to this document.
   - The child timeline links back: each source in a run links to its document page.
3. **Runs gain per-goal SMART ratings for every IEP in the run**, so the IEP page looks the same whether the run was single- or multi-document. Multi-IEP runs need per-document calls plus a synthesis call rather than one long call, so no run hits the output-budget wall that broke IEP analysis on 2026-09-29.
4. **The legacy per-document engine is retired** for IEPs and ETRs: no new `IepAnalysis` / `EtrAnalysis` rows. Consumers that read them (meeting prep, advocate tools, IEP comparison, purge/export) read runs instead.
5. **Existing legacy analyses** are re-synced into runs once more (fixing stale copies like run 15), then treated as read-only history.

Out of scope this pass: progress-report analysis (`ProgressReportAnalysis`) stays on its own engine.

## Why This Approach

- **One source of truth.** Two engines drift: different prompts, different output shapes, different limits, and a one-way, one-time copy between them. Every fix (the 32k budget, refunds on failure) had to be made twice or was missed.
- **Keeps analysis in context.** Parents read an IEP and want its analysis next to it; removing the document-page tab ("child level only") would lose that. Making it a view keeps the context and drops the second engine.
- **Matches the May intent.** `AnalysisRun` was always meant to be the primary entity; this finishes that migration instead of adding links between two systems ("keep both, link them").

## Key Decisions

- **Direction:** one engine (`AnalysisRun`), two views (child page + document page). *(chosen over "keep both, link them" and "child level only")*
- **Scope:** IEPs and ETRs move onto runs. Progress reports stay on their own analysis for now.
- **Document-page view:** shows the latest run that included the document, noting and linking to multi-document runs. *(chosen over "single-document runs only" and a run picker)*
- **Goal ratings:** every IEP in a run gets per-goal SMART ratings; long runs split into per-document calls + a synthesis call. *(chosen over newest-IEP-only and single-IEP-only)*
- **Usage limit:** a run counts as one "analysis" against the per-child limit regardless of how many documents or calls it uses (assumption — matches today's `AnalysisRunService`). Failed runs refund, as they already do.
- **Meeting prep** grounds in the latest completed run that includes the chosen IEP, not in `IepAnalyses` (follows from retiring the legacy engine; builds on PR #35's latest-IEP anchoring).

## Resolved Questions

- *Should the document-page Analysis tab go away?* No — it stays as a view; the engine behind it changes.
- *Do ETRs move too?* Yes, same pass.
- *Do multi-IEP runs carry goal ratings?* Yes, for every IEP.

## Open Questions

- **Legacy tables:** keep `IepAnalyses` / `EtrAnalyses` read-only for one release after the final re-sync, or drop them in the same change? (Plan stage; recommend keep for one release.)
- **Runtime of multi-document runs:** per-document calls may take several minutes each. Does the child page need progress per document ("2 of 3 documents analyzed"), or is the existing "still working" state enough? (Plan / design stage.)
- **Stale view:** when a document is re-processed after its latest run, the document page should say the analysis predates the current parse — reuse the existing stale-analysis banner pattern. (Plan stage.)
- **Prompt parity:** the legacy IEP prompt's section `suggestedQuestions` were deliberately removed from runs in May (meeting prep owns questions). Confirm the run's section output is otherwise at least as useful as the legacy view before the legacy tab is switched over. (Plan / review stage.)
