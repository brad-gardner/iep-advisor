# P4 — Steph, Case Manager / Intervention Specialist

**Type:** Primary
**Status:** Draft — unvalidated
**Journeys:** [J2 School onboarding](../journeys/J2-school-onboarding.md) · [J3 ETR](../journeys/J3-etr-eligibility.md) ◆ · [J4 IEP build](../journeys/J4-collaborative-iep-build.md) ◆ · [J5 School-only](../journeys/J5-school-only-fallback.md) ◆ · [J6 Meeting day](../journeys/J6-meeting-day.md) ◆ · [J7 Progress](../journeys/J7-progress-monitoring.md) ◆ · [J8 Annual review](../journeys/J8-annual-review.md) ◆

> "I became a teacher to teach. I spend more time writing about students than I spend with them."

## Snapshot

| | |
|---|---|
| **Who** | Intervention specialist, middle school; caseload of 24 students; 8th year in the role |
| **Relationship to product** | **The power user.** Authors every IEP and most ETR paperwork on their caseload. If Steph doesn't adopt, nothing else on the school side matters |
| **Also** | Teaches classes. Case management is layered on top of a full instructional load, not instead of it |
| **Tech comfort** | High-competence, low-patience. Fluent in three district systems they resent |
| **Time budget** | Planning period (~45 min), after school (2 hrs), and Sunday evenings. **Deep work happens in stolen blocks** |
| **Device** | School laptop, docked, dual monitor. Phone for nothing work-related |
| **Language** | English |
| **Emotional baseline** | Competent, over-committed, quietly burned out. Protective of students, cynical about systems |

## Why Steph is the fulcrum

Every school-side journey routes through Steph. They write the draft, run the meeting, chase the progress data, and answer for the compliance date. The parent-side product can succeed without them (Mode C), but **the collaborative product cannot exist unless Steph's day gets measurably better.**

The adoption test is blunt: *does this replace time, or add to it?* A tool that improves quality while adding 20 minutes per IEP will be abandoned no matter how good it is — because Steph does not have 20 minutes × 24 students.

## Jobs to be Done

1. *"When I start an IEP, give me a real draft from what we already know, so I'm not starting from a blank template at 9pm."*
2. *"When I write goals, help me write measurable ones fast, so I stop rephrasing the same goal for the eleventh time."*
3. *"Before I finalize, tell me what's missing or non-compliant, so I'm not fixing it after a monitoring visit."*
4. *"When a deadline is coming, tell me early, so I'm never the reason a district is out of compliance."*
5. *"When I need input from the family and the team, collect it for me, so I'm not chasing seven people by email."*
6. *"When the family isn't engaging, let me finish anyway and get the document out — without the tool fighting me."*
7. *"When the meeting ends, capture what we agreed, so I'm not reconstructing it from memory on Thursday."*

## Goals

- **Cut authoring time per IEP substantially** — the only metric that earns adoption
- Produce documents that survive a compliance review without rework
- Never miss a timeline
- Have real family input *before* the meeting so the meeting is a conversation, not a reading
- Spend the reclaimed time on instruction

## Pains & Frictions

- **The blank draft.** Every cycle starts by copying last year's document and editing — which propagates stale goals and last year's mistakes.
- **Goal writing is the bottleneck.** Measurable, ambitious, aligned to present levels, differentiated per student. It's a writing task done 24 times a year under time pressure.
- **Data lives everywhere.** Present levels come from assessments, progress notes, therapist reports, and gradebooks in four systems that don't talk.
- **Chasing input.** Emails to the SLP, the OT, the gen-ed teacher, the parent — each requiring a follow-up.
- **Compliance is a memory game.** Annual dates, three-year re-evaluations, prior written notice, invitation timelines, signature collection. Missing one is a district-level finding with Steph's name on it.
- **The meeting produces changes that must be re-entered.** Handwritten notes → the system, days later.
- **Existing SIS/IEP software is hostile.** Slow, ugly, form-shaped, built for the district's compliance office rather than the person typing.
- **Parent engagement is unpredictable.** Some families are deeply involved; many can't be. Steph plans for both and can't wait for either.

## Context of Use

- **Bursty deep work.** 45-minute planning periods and long after-school blocks. Frequent interruption; work must survive being abandoned mid-sentence.
- **Seasonal load.** Annual reviews cluster; spring is brutal. Re-evaluation season overlays it.
- **Dual monitor, keyboard-driven.** Steph will learn shortcuts if they save time. Mouse-heavy UI is a tax paid 24 times over.
- **Never on a phone.** Any school-side mobile investment is misdirected effort.

## AI Trust Posture

Steph is the persona **most receptive to AI and most punishing of bad AI**. They will happily let AI write a first draft. They will stop using it permanently the first time it invents a fact about a student.

| Wants | Won't tolerate |
|---|---|
| A full draft generated from present levels, prior IEP, and assessment data | **Fabricated student data.** One hallucinated assessment score is a career-level risk |
| Goal language proposed from real baselines, with edit-in-place | Generic goal-bank filler they have to rewrite anyway |
| Compliance/completeness checking before finalize | A tool that blocks them from saving because it disagrees |
| Meeting notes turned into document changes for review | Anything auto-committed to the legal document without approval |
| Suggestions that learn their voice and their district's conventions | Being made to feel supervised by the tool |

**Trust rules:**
- **Every generated statement cites its source** (this assessment, this progress note, this prior goal). Uncited = untrusted.
- **AI proposes; Steph disposes.** Nothing enters the legal document without an explicit human accept.
- **Refuse rather than guess.** "No baseline data available for this area" beats a plausible invented number.
- **Never surface AI output to a parent before Steph has approved it.**

## Language & Accessibility

- English authoring; must produce documents the family reads in *their* language (see P2) without extra work for Steph
- Bilingual collaboration must be **invisible to Steph** — they write English, Rosa reads Spanish, Rosa writes Spanish, Steph reads English. Zero added steps
- Keyboard accessibility is a productivity requirement, not just compliance

## Anti-Goals — what makes Steph abandon

- **Net time added.** The whole case, in three words.
- **Duplicate entry.** Anything already in the SIS that Steph must retype.
- **The tool blocking finalization** because a parent hasn't responded, or because a validation rule disagrees with a professional judgment.
- **Parent-visible drafts they didn't intend to share** — a half-written present-levels statement seen by a family is a genuine professional hazard.
- **Being measured.** Dashboards showing their throughput to administrators turns the tool into surveillance.
- **Slowness.** A save that takes three seconds, 24 students × 12 sections, is the reason they go back to Word.

## Design Implications

1. **The draft starts populated, never blank.** Prior IEP + present levels + assessment data + progress history → a real first draft. This is the single highest-leverage thing the product can do.
2. **Goal authoring is the killer feature and deserves a dedicated, exceptional workspace** — baseline visible, AI proposals inline, one keystroke to accept/edit/reject, alignment checks live. Not a form field in a long page.
3. **Draft visibility is explicit and whole-draft.** Steph shares the complete draft once, deliberately, when it's coherent. Default: nothing visible until then. This is the precondition for Steph tolerating a shared workspace at all — and sharing once beats eight per-section decisions Steph would have to track.
4. **Mode B is not a degraded path — it's a supported path.** Finalize and export a complete, compliant document with zero family participation, no nagging, no blockers, no "waiting on parent" state that can't be cleared. Export must produce something the district can actually use in a traditional review.
5. **Compliance is ambient, not a gate.** Persistent, non-blocking indicators of what's incomplete or approaching a deadline. Steph can always finalize over a warning.
6. **A caseload-shaped home, not a roster.** "What needs you this week" — meetings, drafts due, missing progress data, approaching deadlines — replacing the current student list. Steph's mental model is a *queue*, not a directory.
7. **Input collection is a system job.** The product requests, tracks, and chases input from service providers and families. Steph sees a status, not an inbox.
8. **Autosave, resume, and never lose work.** Bursty interrupted sessions are the norm.
9. **Keyboard-first with visible shortcuts.** Repetition rewards it 24× per cycle.
10. **Post-meeting capture → proposed diffs** Steph reviews and accepts.

## Evidence vs. Assumption

| Claim | Status | How to validate |
|---|---|---|
| Authoring time is the dominant pain and the adoption gate | **Assumption** (strongly supported by special-ed workforce literature) | Time-tracking in pilot; before/after per-IEP |
| Goal writing is the specific bottleneck within authoring | **Assumption** | Ask case managers to rank sections by time spent |
| Steph will not tolerate parent-visible drafts by default | **Assumption** | Direct question during pilot; observe share timing |
| One fabricated data point permanently destroys AI trust | **Assumption** | Watch for abandonment after error events |
| Caseload size ~20–30 is typical | **Assumption** (varies widely by state/district) | Pilot district data |
| Product today has an authoring workspace + draft list + versions | **Evidence** (`iep-authoring`, `iep-versions` features) | — |
| Product today has no compliance timeline or deadline tracking | **Evidence** (no such feature in `web/src/features`) | — |
| Product today has no AI drafting assistance for educators | **Evidence** (AI is parse + analysis only) | — |

## Resolved Decisions

- **Steph authors in our product — we are the system of record.** *(2026-08-03.)* Not an assistive layer beside district IEP software. This removes the double-entry risk that would have been Steph's abandonment trigger, and it means our authoring workspace must be good enough to be someone's whole job, not a helpful supplement.
- **Draft sharing is whole-draft, one deliberate act**, not section by section. *(2026-08-03.)*

## Open Questions

- What does Steph's data actually come from — SIS integration, or entered in our product? This determines whether "draft starts populated" is achievable, and it's now the biggest open question on the school side.
- How much district-specific and state-prescribed template variation must be supported? Unavoidable as system of record.
- When do e-signature and prior-written-notice generation land — launch, or fast-follow?
