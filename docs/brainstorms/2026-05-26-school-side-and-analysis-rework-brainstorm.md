# School-Side IEP Authoring, Analysis Rework, and Student Participation

**Date:** 2026-05-26
**Status:** Brainstorm
**Related:** `docs/brainstorms/2026-04-22-etr-meeting-workflow-brainstorm.md`

## What We're Building

A coordinated rework that turns the platform from a parent-only PDF-analysis tool into a three-sided system: parents (today), school staff (new authors), and students (new self-advocates). Three intertwined changes:

1. **Analysis rework (parent + school).** Replace per-document `IepAnalysis` / `EtrAnalysis` with a new **`AnalysisRun`** entity that spans any combination of IEP + ETR (+ progress reports) selected at run time. Output is sectioned per source document plus cross-doc synthesis. **Meeting prep moves out of analysis entirely** into its own first-class feature.
2. **School-side authoring.** New org hierarchy (**District → School → Teacher**, with a per-student IEP team). Teachers create IEPs as **fully structured data** — every section first-class, goals top-level entities. AI assist appears **inline on key fields** (rewrite this goal, suggest baseline, check measurability) **and** in a **sidebar chat** scoped to the IEP. Workflow is **draft → finalize → share**, where finalize produces an immutable version visible to the parent.
3. **Student self-advocacy workspace.** A separate surface for student-owned content (strengths, interests, accommodations they want, what they want to say at the meeting). Decoupled from the IEP document; teachers and parents can pull excerpts into the IEP or meeting prep. No required student account — invitations come from parent or teacher.

## Why This Approach

- **Separate-but-linked child records** (parent's `ChildProfile` + a school-side `SchoolStudent` with an explicit link) lets us ship the school side without rewriting today's parent-owned model. Avoids merge ambiguity; matches reality where school and family maintain distinct records.
- **`AnalysisRun` over per-doc analyses** unlocks year-over-year and IEP+ETR-together reasoning Claude can't currently do in one prompt, and it generalizes to school-side analysis too.
- **Structured authoring** is the prerequisite for goal-level AI assist and for progress-report alignment that actually means something.
- **Student workspace as separate surface** keeps document mechanics simple while giving the student a real voice — and sidesteps age/consent complexity by not gating the IEP on student credentials.
- **Vertical slices** ship usable value early, prove the data model under load, and let us course-correct before the largest piece (school-side authoring).

## Key Decisions

- **Scope:** All three areas in one brainstorm; all four feature pillars in MVP (`AnalysisRun`, meeting-prep extraction, school authoring, student workspace).
- **Child ownership:** Parent and school each own their own child record; an explicit **link** relationship pairs them so a shared (finalized) IEP becomes visible parent-side. [[child-link-model]]
- **School org:** **District → School → Teacher**, with an **IEP team** (case manager + related service providers) per student.
- **Analysis unit:** **`AnalysisRun`** — user selects any combination of source documents; output is sectioned per source + cross-doc synthesis. Replaces today's `IepAnalysis` / `EtrAnalysis` as the primary analysis entity (existing rows can remain as legacy).
- **Authoring fidelity:** **Full structured IEP** — every section is typed data, goals are a top-level entity (re-parented or duplicated from today's `IepSection`-only relationship).
- **Sharing model:** **Draft → finalize → share** with **immutable version snapshots**. Future edits create a new version; parents always see a stable IEP.
- **Teacher AI surface:** **Inline assists on common fields + sidebar chat scoped to the IEP.**
- **Student participation:** **Self-advocacy workspace, separate from the IEP doc.** Optional student account (invited by parent or teacher); workspace content can be referenced/pulled into IEP and meeting prep.
- **Architecture & sequencing:** **Vertical slices, same end-state architecture.** Slice 1: `AnalysisRun` + meeting-prep extraction (parent-side). Slice 2: school org + structured authoring + draft→finalize→share. Slice 3: student self-advocacy workspace. Each slice ships behind a feature flag and reaches production before the next starts.

## Slice Outline (for planning handoff)

**Slice 1 — Analysis & meeting-prep separation (parent-side first)**
- New `AnalysisRun` entity (child-scoped, references selected source documents, has sectioned + synthesis output JSON).
- Subscription/usage limit semantics updated for "run" unit (replaces per-IEP cap).
- New `/analysis` tab at child level (distinct from per-document Document/Analysis tabs).
- Meeting Prep promoted to its own tab/flow; remove overlap from analysis output (`SuggestedQuestions`).
- Existing per-doc analyses remain readable; new runs are the default.

**Slice 2 — School-side authoring**
- New entities: `District`, `School`, `Teacher` (or `EducatorUser`), `SchoolStudent`, `IepTeamMember`, `ChildLink` (parent ChildProfile ↔ SchoolStudent).
- Structured IEP authoring: top-level `Goal`, typed Section entities with CRUD; versioning via immutable `IepVersion` snapshots on finalize.
- Sharing: finalize creates a parent-visible version; parent links via invite/accept.
- Teacher AI: inline assistants per field + IEP-scoped sidebar chat. Same `AnalysisRun` engine, educator framing.
- Educator role + permissions on `ChildAccess` (or sibling access plane for `SchoolStudent`).

**Slice 3 — Student self-advocacy workspace**
- New `StudentWorkspace` entity scoped to a student identity (which may be a `User` invited by parent or teacher, or a proxy record).
- Content types: strengths, interests, preferred accommodations, "what I want to say at the meeting," AI-led interview answers.
- Reference/pull-in actions from IEP authoring (teacher) and meeting prep (parent).
- Consent/age-gating UX for student account creation.

## Resolved Questions

- **Legacy analyses:** Migrate each existing `IepAnalysis` / `EtrAnalysis` into a single-source `AnalysisRun` so users see all history in one timeline.
- **Goal parent:** `Goal` FKs the immutable `IepVersion` snapshot. A `goal_lineage_id` tracks the same goal across versions for progress-report alignment and year-over-year analysis.
- **Identity model:** Single `User` table with a role discriminator (Parent / Educator / Student / Admin). Per-role profile data goes in side tables (`TeacherProfile`, `StudentProfile`).
- **Parent ↔ school linking:** School-initiated invite by parent email (mirrors today's `ChildAccess` invite flow). Teacher enters parent email when creating `SchoolStudent`; parent receives invite to link or create their `ChildProfile`.
- **Multi-parent / co-parent:** Address in this rework. Drop `ChildProfile.UserId` as the sole-owner field in favor of `ChildAccess` with an `Owner` role that allows multiple parents/guardians.
- **District admin:** Self-serve teacher signup in Slice 2 (educators claim or create a school within a district as org metadata). District admin console deferred until there's a paying district.
- **Billing/usage school-side:** Track `UsageRecord` rows for educator-initiated runs from day one but do not enforce limits in this rework. Defer pricing to market feedback.
- **PDF fallback:** Generate a PDF on finalize and store it alongside the `IepVersion`. Structured data is the source of truth; the PDF is the legal/archive artifact.
- **Co-authoring:** Last-write-wins on field-level edits, no locks. Surface "last edited by X at T" on each field; defer richer concurrency to a follow-up.

## Open Questions

(None blocking — ready for planning.)
