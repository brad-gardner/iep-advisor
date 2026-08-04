# P1 — Dana, Parent (primary)

**Type:** Primary
**Status:** Draft — unvalidated
**Journeys:** [J1 Parent-only adoption](../journeys/J1-parent-only-adoption.md) ◆ · [J2 School onboarding](../journeys/J2-school-onboarding.md) · [J3 ETR](../journeys/J3-etr-eligibility.md) · [J4 IEP build](../journeys/J4-collaborative-iep-build.md) ◆ · [J5 School-only](../journeys/J5-school-only-fallback.md) · [J6 Meeting day](../journeys/J6-meeting-day.md) ◆ · [J7 Progress](../journeys/J7-progress-monitoring.md) · [J8 Annual review](../journeys/J8-annual-review.md) ◆

> "I don't need them to agree with me. I need to stop finding out three months later that I should have asked something."

## Snapshot

| | |
|---|---|
| **Who** | Parent of a 9-year-old with an IEP for a specific learning disability and ADHD; second year in special education |
| **Relationship to product** | The advocate. Often the only person tracking the child across school years, teachers, and buildings |
| **Employment** | Works full time; takes PTO for IEP meetings |
| **Tech comfort** | Everyday-competent. Uses banking and healthcare portals. Not a power user; will not read documentation |
| **Domain expertise** | Learned by necessity and Facebook groups. Knows some acronyms, mistrusts their own understanding of most |
| **Time budget** | 20 minutes at a time, usually at night. A concentrated 2 hours in the week before a meeting |
| **Device** | Phone for checking and reading; laptop for uploading and prep work |
| **Language** | English |
| **Emotional baseline** | Vigilant. Loves the school staff and does not fully trust the process |

## Jobs to be Done

1. *"When a new document lands, help me understand what it actually says, so I know whether to be worried."*
2. *"When I'm preparing for a meeting, tell me what to ask, so I don't leave with regrets."*
3. *"When the year progresses, tell me whether this plan is working, so I'm not taking the school's word for it."*
4. *"When I have to advocate, back me with something specific, so I'm not the emotional parent in the room."*
5. *"When my child changes teachers or schools, carry the history with them, so we don't restart."*

## Goals

- Walk into every meeting **informed and specific** — not confused, not combative
- Know within a day of receiving a document whether it contains anything concerning
- Have a defensible answer to "is my child making progress?" that isn't just a checkbox on a report
- Keep a durable record that survives staff turnover and building changes
- Be treated as a member of the team rather than an audience for a finished plan

## Pains & Frictions

- **The draft arrives finished.** By the time Dana sees it, changing it means objecting to work already done, in a room of professionals, on a clock.
- **The language is a wall.** "Given a grade-level passage, the student will decode multisyllabic words with 80% accuracy across 3 consecutive trials" — is that ambitious, or is it what the child does already?
- **No baseline for comparison.** Nothing tells Dana whether these goals are strong, weak, or boilerplate.
- **Progress reports say "progressing" and nothing else.** Four quarters of "progressing" ending in an unmet goal is a story nobody told them in time.
- **Meetings move fast.** An hour, many people, decisions made in sentences Dana parses afterward in the car.
- **Documents scatter.** Email, backpack, portal, the folder in the kitchen. Nobody has the whole set — including the school.
- **Asking feels expensive.** Every question spends relationship capital with people who will be teaching their child tomorrow.

## Context of Use

- **Trigger-driven, not habitual.** Opens the app when something arrives or a meeting approaches — not on a schedule. Any design assuming daily engagement will fail.
- **Night-time, low-energy, interrupted.** Read on a couch after bedtime, in short bursts, phone in hand.
- **Meeting week is the spike.** Days -7 to 0 is when heavy use happens: reading, prepping, writing down concerns.
- **In the meeting itself,** the phone is face-down on the table. Anything requiring active use during the meeting won't be used.

## AI Trust Posture

| Wants | Won't tolerate |
|---|---|
| Plain-language translation of any document, on demand | AI that hedges everything into uselessness ("consult your team") |
| "What's missing or weak here, compared to what this child needs" | Confident claims about their specific child that turn out wrong in the room |
| A generated list of questions to ask, that they can edit and bring | Anything that would embarrass them if a professional saw it |
| Explanation of rights and process in context, not a legal page | Advice framed as legal advice |
| Being told when something looks *fine* | Anything that reads as auto-generated filler |

**Trust rule:** every AI claim must be traceable to a specific line in a specific document. Dana will quote it in a meeting; if it can't survive that, it shouldn't be shown.

## Language & Accessibility

- English; reads comfortably but not at the technical register special-education documents use
- Target plain-language output at a middle-school reading level, with the original always one tap away
- Phone-first reading: long documents need structure, not a PDF in a viewport

## Anti-Goals — what makes Dana leave

- Feeling like the app is **on the school's side** — school-authored framing, or anything that presents the district's view as neutral fact
- A **subscription that gates the thing they needed** at the moment they needed it
- Being asked to do **data entry the school already has**
- **Notification noise** that trains them to ignore the app before the one message that mattered
- Discovering the app **shared something with the school** they thought was private

## Design Implications

1. **Privacy boundary must be explicit and visible.** Dana's prep notes, AI analysis, and draft concerns are private by default; sharing is a deliberate, labeled act. Every screen that can be seen by the school must say so.
2. **The home surface is meeting-relative, not entity-relative.** "Your meeting is in 6 days — here's what to do" beats a list of children. The current dashboard organizes by data model; Dana organizes by calendar.
3. **Every document needs a one-tap "what does this mean?"** at document, section, and single-goal granularity.
4. **Prep is a first-class artifact**, not a checklist — something Dana builds over a week and walks in holding (printable, shareable at their choice).
5. **Progress must show trajectory, not status.** A goal at 40% in Q3 with a year-end target of 80% is the headline, not "progressing."
6. **Mode C is the on-ramp.** Most Danas arrive with a PDF and no school involvement. Upload → understanding must be minutes, with zero setup ceremony.
7. **When the school joins later, nothing is lost.** Dana's uploaded history and private notes survive the transition to Mode A intact and still private.
8. **Never require in-meeting interaction.** Prep before, capture after.

## Evidence vs. Assumption

| Claim | Status | How to validate |
|---|---|---|
| Parents receive drafts too late to influence them meaningfully | **Assumption** (widely reported in the domain) | Pilot interviews; ask when they first saw the draft |
| Engagement is trigger-driven and spikes pre-meeting | **Assumption** | Analytics on session timing vs. meeting dates |
| Plain-language translation is the highest-value AI feature | **Assumption** | Feature-usage ranking in pilot |
| Parents mistrust "progressing" ratings | **Assumption** | Direct question in pilot interviews |
| Parents will not use the app during a meeting | **Assumption** | Ask post-meeting; check session logs against meeting times |
| The product today supports upload → parse → analysis → prep unassisted | **Evidence** (shipped: children, IEP/ETR upload, sections, goals, analysis, meeting-prep, comparison) | — |
| Parents arrive before their school does | **Assumption** | Signup-source data |

## Open Questions

- Does Dana want to share prep notes with the school ahead of the meeting, or is the value precisely that they're private?
- Is Dana one person or two? (The engaged-solo parent vs. two parents in different households with different access needs.)
- How much does a sibling with a second IEP change the shape of the home surface?
