# P6 — Dennis, School Admin / LEA Representative

**Type:** Secondary
**Status:** Draft — unvalidated
**Journeys:** [J2 School onboarding](../journeys/J2-school-onboarding.md) ◆ · [J3 ETR](../journeys/J3-etr-eligibility.md) · [J4 IEP build](../journeys/J4-collaborative-iep-build.md) · [J5 School-only](../journeys/J5-school-only-fallback.md) · [J6 Meeting day](../journeys/J6-meeting-day.md) ◆ · [J8 Annual review](../journeys/J8-annual-review.md)

> "I'm signing a document that commits district resources, in a meeting I walked into four minutes ago. My job is to make sure nothing happens in that room that we can't stand behind."

## Snapshot

| | |
|---|---|
| **Who** | Assistant principal serving as the district's LEA representative at IEP meetings; also manages the building's special-education staff |
| **Relationship to product** | **Meeting-bound.** Sits in many IEP meetings and signs on behalf of the district. Rarely authors anything |
| **The LEA role** | Federally required. Must be qualified to provide/supervise specially designed instruction, knowledgeable about general curriculum, and **authorized to commit district resources.** That last clause is why Dennis's needs are different from everyone else's |
| **Volume** | Multiple IEP meetings a week during peak season, across the whole building's caseload |
| **Tech comfort** | Moderate. Lives in email, calendar, and the SIS |
| **Time budget** | **Between meetings.** Prep time per meeting is measured in single-digit minutes |
| **Device** | Laptop in meetings; phone between them |
| **Language** | English |
| **Emotional baseline** | Accountable and stretched. Managing risk across many students they don't know individually |

## The distinguishing constraint: commitment authority

Everyone else in the room is discussing what a student needs. Dennis is the person who **binds the district to providing it.** A placement, a one-to-one aide, an out-of-district placement, extended school year — these are budget and staffing commitments made verbally in a meeting and signed the same hour.

That produces one dominant need, which no other persona shares: **no surprises in the room.**

A proposal Dennis first hears in the meeting is a proposal Dennis must either approve without analysis or decline in front of a parent. Both are bad. Everything in Dennis's product experience should serve the goal of arriving *already informed*.

## Jobs to be Done

1. *"Before I walk in, tell me what this meeting is about and what I might be asked to commit to, in two minutes."*
2. *"Flag anything unusual or costly in advance, so I'm never deciding cold in front of a family."*
3. *"Tell me if this meeting is legally sound"* — right participants, proper notice, required elements present.
4. *"Help me run the room"* — keep the meeting on structure so required topics get covered and it ends on time.
5. *"Capture what we agreed, accurately, while we're still in the room."*
6. *"Tell me if a family relationship is going sideways before it becomes a due-process filing."*
7. *"Show me how my building is doing"* — overdue items, staff load, students at risk.

## Goals

- Never be surprised by a resource commitment
- Never sign a document with a procedural defect
- Have meetings end on time with agreements clearly recorded
- Keep family relationships constructive — **due process is the outcome Dennis is paid to avoid**
- Keep the building's compliance clean without micromanaging staff

## Pains & Frictions

- **Walking in cold.** Dennis often gets the draft minutes before, or in the room.
- **Procedural risk is invisible.** Was the parent invited with proper notice? Is a general-education teacher present? Is a district representative qualified for this placement discussion? Any one of those can invalidate a meeting.
- **Meetings run long and skip things.** Discussion of one contentious goal consumes the time meant for transition planning.
- **Verbal agreements are lost.** What was agreed in the room and what appears in the document diverge, and the parent notices.
- **Conflict escalates without warning.** By the time Dennis knows a family is unhappy, an advocate has joined the meetings.
- **Building oversight is manual.** Compliance status assembled by asking staff.
- **No time.** Dennis's entire product interaction is squeezed between other obligations.

## Context of Use

- **Just-in-time, minutes before a meeting.** Any prep artifact must be readable in under two minutes on a phone while walking down a hallway.
- **In the meeting**, laptop open — one of the few personas who *will* actively use the product during the meeting.
- **Between meetings**, triage-mode on a phone.
- **Weekly**, a short building-level scan.

## AI Trust Posture

| Wants | Won't tolerate |
|---|---|
| A pre-meeting brief: what's proposed, what changed, what's unusual, what it may cost | Anything that says something to a family before Dennis has seen it |
| Procedural checks — participants, notice, required elements | A tool that presumes to make legal determinations |
| Live meeting capture → structured agreements for review | Auto-recorded/transcribed content without explicit consent management |
| Early signal that a family relationship is deteriorating | **Anything resembling scoring or profiling a family.** This is a legal and ethical hazard |

**Trust rules:**
- **Advisory only.** Never "this meeting is compliant" — instead "these required elements are not yet documented."
- **Relationship-risk signals must be behavioral and neutral** ("three unanswered messages; parent has requested records twice"), never predictive labels about a family. Get this wrong and it becomes a discovery document in a due-process hearing.
- **Consent is explicit** for any recording or live transcription, and visible to everyone in the room.

## Language & Accessibility

- English; must be able to see what the family is seeing in their language (see P2) to keep the meeting synchronized
- Two-minute-readable briefs; density over completeness

## Anti-Goals — what makes Dennis disengage

- **A tool that creates a record that hurts the district.** Dennis will actively block adoption if the product generates discoverable material that increases legal exposure.
- **Surprises reaching parents first.** A parent seeing a draft commitment before Dennis does is a serious problem.
- **Another dashboard to check.** Dennis has several already.
- **Being made the bottleneck** — approval workflows that stall Steph's work will get Dennis's cooperation withdrawn.
- **Anything that looks like family surveillance** or staff-productivity monitoring.

## Design Implications

1. **The pre-meeting brief is Dennis's product.** One artifact, two-minute read, phone-friendly, auto-generated, delivered on a schedule (e.g. the evening before and one hour prior): what's proposed, what changed from last year, resource implications, procedural gaps, family input received.
2. **Flag resource-commitment items explicitly.** Placement changes, new services, one-to-one support, ESY, out-of-district — these are Dennis's decision surface and should be visually distinct everywhere they appear.
3. **Procedural completeness is a checklist Dennis can see before signing** — participants present, notice documented, required sections complete. Advisory, never blocking, never phrased as a legal conclusion.
4. **In-meeting capture is a real surface** — the only one designed for active use during a meeting. Agenda structure, timing, agreements captured as they're made, converted to proposed document changes afterward.
5. **Relationship-health signals must be behavioral, neutral, and defensible in discovery.** Design this with counsel input, or don't ship it.
6. **Building-level oversight is a scan, not a workspace** — one view, exception-driven, no drill-down required.
7. **Dennis's view is read-mostly.** They observe, verify, and sign; they don't author. The current design gives all Educators the same authoring-shaped surface — wrong for Dennis.
8. **Signature/commitment is a distinct, deliberate moment** with a summary of exactly what's being committed to.

## Evidence vs. Assumption

| Claim | Status | How to validate |
|---|---|---|
| LEA reps routinely walk into meetings without having read the draft | **Assumption** (widely reported) | Ask directly in pilot; measure brief open rates |
| "No surprises about resource commitments" is the dominant need | **Assumption** | Rank-order interview with school administrators |
| Procedural defects are a common and costly failure | **Evidence** (well established in special-education law) | — |
| Administrators will use the product during meetings | **Assumption** | Session logs vs. scheduled meeting times |
| Relationship-risk signals would be welcomed rather than feared | **Assumption — high risk** | Test explicitly with district counsel before building |
| The LEA rep must be authorized to commit district resources | **Evidence** (IDEA statutory requirement) | — |
| Product today gives school admins the same surface as case managers | **Evidence** (single `Educator` role; `/educator/*` shell shared) | — |
| Product today has no meeting-day surface for any school persona | **Evidence** (meeting-prep is parent-side only) | — |

## Open Questions

- Is the school admin distinct enough from the district director (P7) to stay separate, or do they merge once the oversight dashboard exists?
- Does relationship-risk signaling belong in the product at all? The legal exposure may exceed the value — this needs a real decision, not a default.
- E-signature and the formal signature page are in scope now that we're the system of record (decided 2026-08-03) — Dennis's commitment moment becomes a real product surface. When does it land?
- Who is the LEA rep when Dennis isn't available? The role is a hat, not a job title — should the product model it as an assignable role per meeting rather than a user attribute?
