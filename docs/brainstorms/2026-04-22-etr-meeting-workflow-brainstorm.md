# ETR Meeting Workflow — Brainstorm

**Date:** 2026-04-22
**Status:** Brainstorm (pre-plan)
**Related:** Parallels existing IEP document workflow

## What We're Building

A parallel workflow for **Evaluation Team Reports (ETRs)** that mirrors the existing IEP document experience. Parents will be able to create an ETR for a child, upload a draft or prior ETR document, and receive the same depth of AI-assisted breakdown, analysis, red flags, suggested meeting questions, and meeting-prep support they get for IEPs.

The motivating use case: a parent has an ETR meeting tomorrow with only a draft in hand (or sometimes just a prior/outdated ETR). They need fast, trustworthy help understanding what it says, what's missing, and what to ask.

### Core user flow
1. Parent selects a child.
2. Creates a new ETR entry (meeting date, draft vs. final, eligibility category under consideration, notes).
3. Uploads the ETR PDF.
4. Background workers parse the document into structured ETR sections and run analysis.
5. Parent views a breakdown with plain-language summaries, assessment completeness check, eligibility-determination review, red flags, and a meeting-prep checklist / suggested questions.
6. Parent can upload multiple ETRs over time (draft, final, reevaluations) — each listed chronologically under the child.

## Why This Approach

The IEP workflow is already mature: upload → queue → parse via Claude → structured sections → analysis → meeting prep. Reusing that proven architectural shape minimizes risk and lets us ship a feature of real parity quickly. ETRs are distinct enough in content (assessment data, eligibility determinations — not goals/services) that forcing them into `IepDocument` would introduce awkward null fields and branching. Parallel entities keep each workflow understandable and let ETR-specific analysis prompts evolve independently.

## Key Decisions

- **Data model:** Separate parallel entities — `EtrDocument`, `EtrSection`, `EtrAnalysis`, mirroring the IEP triplet. Reuse repository/service patterns from IEP. Same async worker model (processing queue + analysis queue).
- **Navigation:** Separate top-level ETR section in the app, parallel to IEP. (User chose this over a per-child document tab — explicit separation matches how parents think about these distinct meetings.)
- **Scope:** Full parity with IEP on first release — upload, parse, analysis, red flags, suggested questions, and meeting-prep checklist.
- **Versioning:** Multiple ETRs per child with a timeline view. Each ETR is its own document (status: draft / final) with its own analysis. No forced single-active constraint; no auto-comparison in v1.
- **Analysis pillars (ETR-specific):**
  1. **Assessment completeness** — are all relevant evaluation domains covered (cognitive, academic, behavioral, adaptive, communication, OT/PT, etc.)? Flag missing areas of suspected disability.
  2. **Eligibility determination review** — does the assessment data support the stated (or proposed) eligibility category? Are conclusions consistent with the testing?
  3. **Red flags & parent rights** — outdated testing, boilerplate, procedural issues, under-evaluation.
  4. **Meeting questions** — targeted questions the parent should ask the ETR team based on identified gaps.
- **Document processing:** Same Claude-based extraction pipeline; new ETR-specific section taxonomy (e.g., background info, assessments administered, results by domain, eligibility determination, team recommendations) and new analysis prompts.
- **Child linkage:** ETRs belong to a `ChildProfile` the same way IEPs do.

## Open Questions

_(None blocking — move to planning.)_

## Resolved Questions

- **Separate entities vs. extend IepDocument?** → Separate parallel entities.
- **UI placement?** → Separate top-level ETR section.
- **MVP scope?** → Full parity with IEP workflow.
- **Multiple ETRs per child?** → Yes, chronological timeline; no draft-vs-prior comparison in v1.
- **Analysis focus?** → All four pillars (completeness, eligibility, red flags, questions).

## Non-Goals (v1)

- Automated comparison between a draft ETR and a prior ETR.
- ETR-to-IEP cross-analysis (e.g., checking whether the resulting IEP goals align with ETR findings).
- Supporting 504 plans, BIPs, or other evaluation document types (ETR architecture should stay clean enough to inform those later, but we're not abstracting for them now).
- Editing or annotating the ETR document in-app.
