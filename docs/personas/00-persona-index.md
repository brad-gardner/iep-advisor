# Persona Index

**Created:** 2026-08-03
**Status:** Draft — synthesized from the special-education domain and the current product. **Unvalidated.** Every persona carries an Evidence-vs-Assumption ledger; treat unmarked behavior as hypothesis until pilot data says otherwise.
**Companion:** [`docs/journeys/`](../journeys/00-journey-index.md)

## How to use these

These are **design inputs, not decoration**. A screen, flow, or feature should be traceable to at least one persona's job-to-be-done and at least one journey stage. When a proposal doesn't map to either, that is a signal to ask why we're building it — not a signal to add a persona.

Each persona file follows the same format:

| Section | What it's for |
|---|---|
| Snapshot | Fast orientation: context, constraints, device, language, time budget |
| Jobs to be Done | What they hire the product to accomplish, in their words |
| Goals | Outcomes they'd call success |
| Pains & Frictions | What currently makes this hard — in the world, not just in our app |
| Context of Use | When, where, on what, under what pressure |
| AI Trust Posture | What AI help they want, and what would break their trust |
| Language & Accessibility | Reading level, language, assistive-tech needs |
| Anti-Goals | What would make them abandon the product |
| Design Implications | The implementable consequences — this is the payload |
| Evidence vs. Assumption | Honest ledger of what we know vs. what we guessed |

## The roster

| # | Persona | Type | One-line | File |
|---|---|---|---|---|
| P1 | **Dana** — Parent (primary) | Primary | Determined, out-gunned; wants to walk into the meeting as an equal | [parent-primary.md](parent-primary.md) |
| P2 | **Rosa** — Parent (multilingual) | Primary | Same fight, conducted through an interpreter who arrives late | [parent-multilingual.md](parent-multilingual.md) |
| P3 | **Alex** — Student (transition-age) | Secondary | Being planned *about*; wants to be planned *with* | [student-transition.md](student-transition.md) |
| P4 | **Steph** — Case manager | Primary | Authors everything, drowning in paperwork, the platform's power user | [case-manager.md](case-manager.md) |
| P5 | **Priya** — Service provider | Secondary | Owes progress data across many caseloads; wants in and out in 10 minutes | [service-provider.md](service-provider.md) |
| P6 | **Dennis** — School admin / LEA rep | Secondary | Signs on behalf of the district; runs the room; needs the meeting to hold | [school-admin-lea.md](school-admin-lea.md) |
| P7 | **Karen** — District SpEd director | Primary (buyer) | Answers for compliance; the one who signs the contract | [district-sped-director.md](district-sped-director.md) |
| P8 | **Sam** — Platform admin | Internal | Keeps the platform trustworthy and unblocks everyone else | [platform-admin.md](platform-admin.md) |

**Primary** = we optimize for them and accept trade-offs against others. **Secondary** = must be well-served but doesn't drive core design. **Internal** = our own operations.

### Why the school side is four personas

"Educator" is a single role in the database and four different jobs in reality. Collapsing them is precisely why the school surface currently reads as one undifferentiated shell — everyone gets the same roster→student→tabs experience regardless of whether they author IEPs all day or log a progress note twice a quarter.

| | Steph (case manager) | Priya (service provider) | Dennis (school admin) | Karen (district director) |
|---|---|---|---|---|
| **Primary verb** | Author | Report | Facilitate & sign | Oversee |
| **Session length** | Hours | Minutes | Meeting-bound | Scanning |
| **Cares about** | Draft quality, time saved | Speed, low friction | The meeting holding together | Compliance, risk, adoption |
| **Failure that hurts** | Rewriting from scratch | Being nagged | Surprise in the room | An audit finding |

## Persona ↔ Journey matrix

Legend: **◆** drives the journey · **○** participates · *(blank)* not involved

| | J1 Parent-only | J2 School onboard | J3 ETR | J4 IEP build | J5 School-only | J6 Meeting | J7 Progress | J8 Annual |
|---|---|---|---|---|---|---|---|---|
| P1 Dana | ◆ | ○ | ○ | ◆ | ○ | ◆ | ○ | ◆ |
| P2 Rosa | ◆ | ○ | ○ | ◆ | ○ | ◆ | ○ | ◆ |
| P3 Alex | | | ○ | ○ | | ○ | ○ | ○ |
| P4 Steph | | ○ | ◆ | ◆ | ◆ | ◆ | ◆ | ◆ |
| P5 Priya | | ○ | ○ | ○ | ○ | ○ | ◆ | ○ |
| P6 Dennis | | ◆ | ○ | ○ | ○ | ◆ | | ○ |
| P7 Karen | | ◆ | ○ | | ○ | | ○ | ○ |
| P8 Sam | ○ | ○ | | | | | | |

## Standing decisions

Decided 2026-08-03. Each persona's file records the consequences for that persona under **Resolved Decisions**.

- **We are the system of record** — we author and hold the legal IEP/ETR, replacing district IEP software rather than layering on it. Biggest impact on [Steph (P4)](case-manager.md) and [Karen (P7)](district-sped-director.md).
- **The school shares drafts whole**, in one deliberate act, not section by section.
- **Validation:** pilot interviews plus advisory review by a special-ed director, a case manager, and a parent advocate — before these assumptions become features.

## The three operating modes

Every persona and journey is written against a product that must serve all three equally:

- **Mode A — Both sides.** School and family both on the platform. Shared workspace, structured parent/student input, bilingual collaboration. The flagship.
- **Mode B — School-only.** The family doesn't participate. The school completes a compliant document and **exports** it for traditional review. Non-participation must degrade gracefully and never block or slow the school.
- **Mode C — Parent-only.** The school isn't on the platform. The parent uploads their own IEPs and ETRs and independently understands, tracks, and advocates. This is today's shipped product and it stays first-class.

A design that only works in Mode A is not done.

## Maintenance

- Personas change when **evidence** changes, not when features change. Update the ledger, not the fiction.
- If a real user doesn't fit any persona, that's data — record it before inventing P9.
- Keep the roster at 8. A persona set nobody can hold in their head has stopped being a design tool.
