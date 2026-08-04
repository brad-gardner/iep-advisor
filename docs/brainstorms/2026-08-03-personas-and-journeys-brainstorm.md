# User Personas & Journeys — Brainstorm

**Date:** 2026-08-03
**Status:** Draft
**Precedes:** `/sht:plan`
**Builds on:** the three prior UX passes — design tokens (2026-07-01), primitive migration (2026-07-01/02), page composition & IA (2026-07-02).

## Context

Three consecutive UX passes fixed the *building blocks*: tokens, then primitives (`Modal`/`Drawer`/`Table`/`ConfirmDialog`/`DetailLayout`), then page composition and density. Each was a real improvement, and none of them asked the prior question: **who is this for, and what are they trying to get done?**

The result is an app that is internally consistent but organized around *entities* (children, documents, goals, versions, schools, staff) rather than around *work*. Navigation mirrors the data model. Every role gets a variant of the same list→detail→tab shell. There is no articulated notion of what a case manager's Tuesday looks like, or what a parent needs in the 48 hours before a meeting.

No persona or journey artifacts exist anywhere in the repo — no interview notes, no pilot feedback, no analytics. This pass creates that missing layer, deliberately **at vision scope**: the current codebase is a floor, not a ceiling.

## What We're Building

Two new documentation trees that become the shared reference for all subsequent UX and product work:

- **`docs/personas/`** — 8 personas, one file each, in a standard format (snapshot, jobs-to-be-done, goals, pains, context of use, AI trust posture, anti-goals, design implications, evidence-vs-assumption ledger).
- **`docs/journeys/`** — 8 journeys, one file each, written as **cross-persona swimlanes** so multi-party flows stay canonical instead of being retold from each side and drifting.

Both directories carry an index that cross-links personas ↔ journeys.

### The product vision these are written against

A comprehensive platform where **schools and parents collaboratively build and manage IEPs**, with AI assistance throughout and full multilingual support. Concretely:

**We are the system of record.** The platform authors and holds the legal IEP and ETR — it does not sit beside a district's existing IEP software. This is the highest-consequence decision in the set: it removes the double-entry risk that would have made the school side unusable for Steph, and it commits us to state-prescribed form fidelity, e-signature, prior written notice, migration from incumbent systems, and the operational bar of a system a district's compliance depends on.

**Collaboration model — shared workspace with scoped rights.** One living IEP document all parties see at the same time. The school holds authorship and legal sign-off; parents and students contribute through **structured, first-class slots** (parent concerns, student vision, input on proposed goals) that are part of the document, not comments bolted to the side. The school shares the draft **as a whole**, once it's coherent — not section by section — so the family sees a complete picture they can actually analyze and respond to.

**Three operating modes the product must serve equally well:**

| Mode | Situation | What the platform must guarantee |
|---|---|---|
| **A — Both sides** | School and family both on the platform | The flagship experience: shared workspace, structured parent/student input, live bilingual collaboration |
| **B — School-only** | Family doesn't participate (or can't) | The school can complete a compliant document and **export it out** for traditional paper/email review. Parent non-participation must never block or degrade the school's work |
| **C — Parent-only** | School isn't on the platform yet | The parent uploads their own IEPs/ETRs and fully manages, understands, and advocates independently. This is today's product and it stays first-class |

**ETRs are managed on both sides** — evaluation and eligibility are part of the platform's scope, not just the IEP that follows.

**AI posture — all four roles:**
1. **Translator + explainer** — dense IEP/ETR language into plain language, in the reader's language
2. **Drafting copilot for educators** — goal language from present-levels data, accommodation suggestions, completeness/compliance checks
3. **Advocate + prep coach for parents** — weaknesses in an IEP, questions to ask, meeting preparation
4. **Progress + compliance watchdog** — off-track goals, approaching timelines, stale progress reporting

**Multilingual — full bilingual collaboration.** Each party reads and writes in their own language; translation runs in both directions across the shared workspace, including live during the meeting. Not a parent-side veneer over an English system.

## Why This Approach

- **Separate journeys directory.** The flagship journey (co-building an IEP) belongs to no single persona. Embedding journeys inside persona files would mean writing that flow three or four times from different vantage points — and those retellings would drift apart within a release. Swimlane journeys keep one canonical narrative; persona files link to the journeys they appear in.
- **Educator split into four.** "Educator" is one database role but four different jobs: the case manager who authors and is drowning in paperwork, the service provider who owes progress data and little else, the school admin who is the LEA representative in the room, and the district director who answers for compliance. A single "Educator" persona is exactly why the school surface currently reads as one undifferentiated shell.
- **Vision scope, not current-state scope.** Personas written against today's features would encode today's limits. Each journey instead carries an explicit **"Gap vs. today"** section, which is where the implementable backlog actually comes from.
- **Evidence-vs-assumption ledger in every persona.** With zero research on hand, the honest move is to synthesize from the special-education domain and mark every behavioral claim as `Assumption` until the pilot validates it. This keeps useful fiction from hardening into product truth.

## Key Decisions

- **Scope: all four role surfaces**, with **Educator split into four sub-personas** — 8 personas total. *(User-chosen.)*
- **Structure: `docs/personas/` + `docs/journeys/`**, cross-linked, one file each, standard format. *(User-chosen.)*
- **Grounding: synthesized from domain + codebase, every claim tagged Evidence or Assumption**, written at **vision scope** — the current codebase is explicitly not a limiting factor. *(User-chosen.)*
- **We are the system of record** — we author and hold the legal IEP/ETR, replacing district IEP software rather than layering on it. *(User-chosen.)*
- **Collaboration model: shared workspace with scoped rights**, school retains authorship and sign-off. *(User-chosen.)*
- **Draft sharing is whole-draft, one deliberate act** — not section-by-section. Keeps the back-and-forth manageable and gives the family a complete picture to analyze. *(User-chosen.)*
- **Validation: pilot interviews + advisory review** by a special-ed director, a case manager, and a parent advocate. *(User-chosen.)*
- **Three operating modes are all first-class** — both-sides, school-only-with-export, parent-only. Parent non-participation must never block the school. *(User-chosen.)*
- **ETRs are in scope on the school side**, not just parent-side uploads. *(User-chosen.)*
- **AI plays all four roles** — translator/explainer, educator drafting copilot, parent advocate/prep coach, progress + compliance watchdog. *(User-chosen.)*
- **Multilingual: full bilingual collaboration**, both directions, including live at the meeting. *(User-chosen.)*
- **Persona roster (8):** Parent (primary), Parent (multilingual), Student (transition-age), Case manager, Service provider, School admin / LEA rep, District SpEd director, Platform admin. *(User-chosen.)*
- **Journey roster (8):** J1 parent-only adoption, J2 school onboarding, J3 ETR & eligibility, J4 collaborative IEP build, J5 school-only fallback, J6 meeting day, J7 progress monitoring, J8 annual review.
- **Pronouns:** all personas use they/them. Personas are composites; assigning gender adds nothing and invites stereotype.

## Deliverables

```
docs/personas/                     docs/journeys/
  00-persona-index.md                00-journey-index.md
  parent-primary.md                  J1-parent-only-adoption.md
  parent-multilingual.md             J2-school-onboarding.md
  student-transition.md              J3-etr-eligibility.md
  case-manager.md                    J4-collaborative-iep-build.md
  service-provider.md                J5-school-only-fallback.md
  school-admin-lea.md                J6-meeting-day.md
  district-sped-director.md          J7-progress-monitoring.md
  platform-admin.md                  J8-annual-review.md
```

## Resolved Questions

1. **Where do journeys live?** Separate `docs/journeys/` directory as cross-persona swimlanes — multi-party flows stay canonical.
2. **Is "Educator" one persona or several?** Four: case manager, service provider, school admin/LEA, district director.
3. **Current-state or vision-state?** Vision-state, with a mandatory "Gap vs. today" section per journey.
4. **How deep is multilingual?** Full bilingual collaboration in both directions, including live at the meeting.
5. **What if the parent doesn't participate?** Mode B is a first-class requirement: the school completes and **exports** the document for traditional review. Non-participation degrades gracefully and silently.
6. **System of record or a layer beside district IEP software?** **System of record.** We author and own the legal document. Recorded as a Resolved Decision in P4, P7, J3, J4, and J5, where the consequences differ.
7. **How much of the draft may a parent see, and when?** The **whole draft**, once the school deliberately shares it. Not section-by-section — partial documents can't be analyzed and multiply the review cycles.
8. **Validation method?** **Pilot interviews + advisory review** — a special-ed director, a case manager, and a parent advocate read the personas and mark what's wrong before the assumptions become features.

## Open Questions

1. **Sequencing** — which journey is the first implementation target? Deferred; the leading candidates are J1 (highest gap-to-effort, no district dependency, owns the acquisition funnel) and the **timeline/deadline model**, which isn't a journey but is the prerequisite for four of the eight and now doubly so as system of record.
2. **Export and form fidelity** — as system of record, state-prescribed IEP form output is unavoidable. How much variance must be supported, and when? This is now the largest unscoped engineering question. (See [J5](../journeys/J5-school-only-fallback.md).)
3. **SIS integration** — "the draft is never blank" depends on present-levels and roster data we don't generate. Integrate, import, or enter?
4. **Displacement** — replacing an incumbent IEP system means migration of existing documents, contract timing, and retraining. What does that sale actually look like? (See [P7](../personas/district-sped-director.md).)
5. **Org-level policy switch** — should a district be able to disable family draft visibility entirely, putting all its students in Mode B by policy? Raised by both P6 and P7 as a potential procurement blocker; still unanswered.

## Out of Scope

Visual design or component work (the three prior passes cover that layer); backend/schema design; the marketing site's persona targeting; pricing and packaging; SIS/SSO integration strategy.
