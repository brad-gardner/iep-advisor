# P3 — Alex, Student (transition-age)

**Type:** Secondary
**Status:** Draft — unvalidated
**Journeys:** [J3 ETR](../journeys/J3-etr-eligibility.md) · [J4 IEP build](../journeys/J4-collaborative-iep-build.md) · [J6 Meeting day](../journeys/J6-meeting-day.md) · [J7 Progress](../journeys/J7-progress-monitoring.md) · [J8 Annual review](../journeys/J8-annual-review.md)

> "They spent an hour talking about me while I sat there. Someone asked what I wanted to do after school and I said 'I don't know' because I'd had four seconds to think about it."

## Snapshot

| | |
|---|---|
| **Who** | 16-year-old, 10th grade, IEP since 4th grade; goals in reading comprehension, written expression, and self-advocacy |
| **Why they matter** | Federal law requires the student be invited to the IEP meeting from age 16 (often 14 by state rule) whenever transition is discussed. Attendance is near-universal; **meaningful participation is not** |
| **Tech comfort** | Native. Highest of any persona. Also the least patient with bad software |
| **Time budget** | Minutes. Attention is contested by everything else on the phone |
| **Device** | Phone. Occasionally a school-issued Chromebook |
| **Language** | English (may differ from the home language — see Rosa, P2) |
| **Emotional baseline** | Guarded. Special education is something that happens to them, in a building where being pulled out is socially visible |

## The design problem, stated plainly

Alex is the only persona **who is the subject of the document** and the only one who currently has almost no product surface (one page: `/student`). Every other persona acts on Alex's behalf.

Two failure modes are equally bad:

- **Tokenism** — Alex gets a read-only view of a document written about them in language they can't parse, and a seat at a meeting where they're asked one question near the end.
- **Overload** — Alex is handed the full clinical IEP and told to engage with it. They won't. It's boring, it's about deficits, and it reads like a report card written by strangers.

The target is neither. It's **giving Alex something worth showing up for**: a stake in decisions that visibly affect their actual life — classes, accommodations, what happens after graduation.

## Jobs to be Done

1. *"Tell me what this plan actually means for my school day, so I know what to expect."*
2. *"Let me say what I want before the meeting, so I'm not put on the spot."*
3. *"Let me tell them which accommodations help and which are embarrassing"* — the extra time helps; being walked out of class in front of everyone does not.
4. *"Help me talk about what I want after high school, when I don't know yet."*
5. *"Show me I'm getting somewhere"* — progress framed as capability gained, not deficit remaining.
6. *"Let me ask something without an adult watching."*

## Goals

- Not be surprised in the meeting
- Have at least one thing they said actually change the plan
- Understand their own accommodations well enough to ask a teacher for them
- Leave high school able to explain their needs to a boss or a college disability office — **the actual point of transition planning**

## Pains & Frictions

- **The document is about deficits.** Reading a professional inventory of what you can't do is not motivating.
- **Nobody prepared them for the question.** "What are your goals after graduation?" asked cold, in front of six adults, produces "I don't know."
- **Accommodations are decided without them** — including which ones are socially costly to use.
- **The vocabulary excludes them** as thoroughly as it excludes Dana, with less standing to ask.
- **Progress is invisible.** Percentages against a goal set a year ago mean nothing at 16.
- **The meeting is adult-shaped**: an hour, a table, professional register, mid-school-day.

## Context of Use

- Phone, short bursts, between other things
- **Privacy matters intensely.** Alex will not engage honestly if a parent sees every keystroke. This directly conflicts with parents' expectations and must be designed, not defaulted
- Won't return to a slow or ugly product; the bar is consumer apps, not school software
- Best engagement window: prompted, days before a meeting, with something concrete to do

## AI Trust Posture

| Wants | Won't tolerate |
|---|---|
| Plain, direct explanations without condescension | Being talked down to, or "Hey Alex! 🎉 Let's crush your goals!" |
| Help turning a vague feeling into something sayable in a meeting | AI that reports back to adults |
| A private place to ask "what does *emotional disturbance* mean on my file?" | Anything that looks like surveillance |
| Rehearsal — practice saying what they want | Cartoon gamification |

**Trust rule:** Alex's private drafts stay private until Alex shares them. If a school or parent can ever see something, say so *on the input*, before they type — not in a settings page.

## Language & Accessibility

- Plain language throughout; direct, not softened, not juvenile
- Reading support matters — Alex's IEP is partly *for* reading. Text-to-speech, short chunks, no walls of clinical prose
- Voice and short-form input over long typing
- Home language may differ from Alex's own preferred language (see P2)

## Anti-Goals — what makes Alex disengage

- **Homework.** Anything that feels assigned by school.
- **Being watched.** Any hint that a parent or teacher sees their private input.
- **Cringe.** Fake enthusiasm, mascots, points, streaks.
- **Powerlessness.** Contributing input and seeing nothing change is worse than never being asked.
- **Deficit framing.** A dashboard of what's wrong with them.

## Design Implications

1. **Alex's surface is a preparation and voice tool, not a document viewer.** The IEP is the *source*; the product is what Alex does with it.
2. **A real privacy model with three tiers** — private to Alex / shared with the team / part of the document. Visible at the point of input, chosen per item. This is the single highest-stakes design decision on this surface.
3. **The student vision statement is a guided, low-pressure, asynchronous build** — a few questions over a few days, not one blank box before a meeting. Produces something Alex can hand over or read aloud.
4. **Accommodations get a student-facing view with feedback**: "does this help? do you actually use it?" — the most actionable input Alex can give and nobody currently asks for.
5. **Progress framed as capability**, in their words: "you can now read and summarize a chapter on your own" — not "62% of benchmark."
6. **Rehearsal before the meeting** — here's what will be discussed, here's what you said you wanted, here's how you might say it.
7. **Age-gate the surface.** A 16-year-old's product is not a 9-year-old's. If younger students are ever included, that's a distinct persona.
8. **Age of majority is a hard product event.** At 18, rights transfer to the student in most states. The permission model must anticipate a student becoming the primary rights-holder.
9. **Phone-native, fast, adult in tone.** Consumer-quality or it won't be opened twice.

## Evidence vs. Assumption

| Claim | Status | How to validate |
|---|---|---|
| Students attend but rarely participate meaningfully | **Assumption** (widely reported in transition literature) | Ask case managers and students in pilot |
| Privacy from parents is a precondition for honest student input | **Assumption** | Direct question; compare input volume under different visibility settings |
| Accommodation feedback is the highest-value student contribution | **Assumption** | Pilot: measure whether student feedback changes the document |
| Students won't engage with the clinical document directly | **Assumption** | Observe usage of raw-document vs. plain-language views |
| Product today gives students one page and no participation surface | **Evidence** (`/student` = `StudentHomePage` only) | — |
| Student invite flips the user's role to Student | **Evidence** (`student-accept-invite-page`, role routing) | — |
| Transition-age (14–18) is the right scope | **Assumption** | Pilot demographics; district policy |

## Open Questions

- Where exactly is the privacy line, and who decides it — us, the district, or the parent? What happens when a parent demands to see their minor child's private notes?
- Does age of majority (18) need to be modeled now, or can it wait?
- Is there a younger-student persona (elementary self-advocacy), or is transition-age the only student we serve?
- Does Alex get their own login, or is this a mode inside the parent account? (Today it's a separate role — worth re-deciding.)
