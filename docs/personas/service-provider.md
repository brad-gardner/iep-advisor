# P5 — Priya, Service Provider / Contributing Teacher

**Type:** Secondary
**Status:** Draft — unvalidated
**Journeys:** [J2 School onboarding](../journeys/J2-school-onboarding.md) · [J3 ETR](../journeys/J3-etr-eligibility.md) · [J4 IEP build](../journeys/J4-collaborative-iep-build.md) · [J5 School-only](../journeys/J5-school-only-fallback.md) · [J6 Meeting day](../journeys/J6-meeting-day.md) · [J7 Progress](../journeys/J7-progress-monitoring.md) ◆ · [J8 Annual review](../journeys/J8-annual-review.md)

> "I have 52 students across four buildings. I am not learning another system. Send me a link, let me type three sentences, and let me go."

## Snapshot

| | |
|---|---|
| **Who** | Speech-language pathologist serving four schools; ~52 students on service. Stands in for the whole class of contributing professionals — SLPs, OTs, PTs, school psychologists, and general-education teachers |
| **Relationship to product** | **Occasional contributor, never an owner.** Owes progress data, service minutes, and section input to IEPs authored by many different case managers |
| **Caseload spread** | Students belong to different case managers in different buildings. Priya is a spoke on many hubs |
| **Tech comfort** | Competent. Has zero appetite for another login |
| **Time budget** | **Minutes, between sessions.** Documentation happens in 5-minute gaps and a Friday-afternoon catch-up block |
| **Device** | Laptop, often on a cart or in a borrowed room. **Phone/tablet is realistic here** — unlike Steph, Priya moves all day |
| **Language** | English |
| **Emotional baseline** | Stretched thin, professionally proud, chronically behind on paperwork |

## Why Priya matters more than their session count suggests

Priya is the **source of the data everything else depends on**. Progress monitoring (J7), present levels (J4), and the honesty of the annual review (J8) all rest on service providers reporting real data on time. Today that reporting is chased by email, arrives late, and is often written from memory the night before a meeting.

But Priya's engagement ceiling is *low by necessity*. The design goal is not to increase Priya's usage — it's to **extract high-quality input at the lowest possible cost per interaction.** Every minute of friction is multiplied by 52.

## Jobs to be Done

1. *"Tell me exactly what you need from me and when it's due, so I don't have to track it."*
2. *"Let me log a session note in under a minute, right after the session, so I'm not reconstructing 52 students on Friday."*
3. *"Turn my session notes into the progress report, so I'm not writing the same thing twice."*
4. *"Let me contribute my section without opening the whole IEP."*
5. *"Tell me when a student I serve is off track, so I can adjust before the annual review."*
6. *"Let me attend the part of the meeting that concerns me and skip the rest."*

## Goals

- Get documentation obligations off their plate quickly and completely
- Never be the reason a meeting is delayed or an IEP is incomplete
- Have their clinical judgment appear in the document intact, not paraphrased away by a case manager
- Spend time with students rather than in forms

## Pains & Frictions

- **Chased by email.** Requests arrive from many case managers in different formats with different deadlines, buried in a full inbox.
- **Recall-based documentation.** Notes written days later are less accurate and take longer to write.
- **Repetition.** Session notes, progress reports, and present-levels input restate the same observations three times in three places.
- **No context on request.** "Can you send me your input for Jordan?" — for which goals? in what format? by when?
- **Being asked to log into a system for a two-sentence contribution.**
- **Meeting time is expensive.** Sitting through 45 minutes for the 4 minutes that concern them, ×many meetings.
- **Mobility.** Working across buildings from carts and borrowed rooms, often without a reliable place to sit and type.
- **Fragmentation.** Their students are spread across case managers, so no single roster view reflects their actual work.

## Context of Use

- **Micro-sessions.** 60–180 seconds, immediately after a student session, standing up, in a hallway
- **One weekly catch-up block**, typically Friday afternoon, to clear the backlog
- **Mobile is legitimate here** — this is the one school-side persona where a phone/tablet interface earns its keep
- **Interruption-prone.** Must survive being abandoned mid-entry
- Notifications are the primary entry point: Priya does not go looking for work in our app

## AI Trust Posture

| Wants | Won't tolerate |
|---|---|
| Session notes → drafted progress report they review and send | AI writing clinical judgments they didn't make |
| Voice note → structured entry | Having to correct more than they'd have written |
| A prompt for the *right* data at the right moment | Being nagged |
| Trend detection across sessions they'd have missed | Their name attached to text they didn't approve |

**Trust rules:**
- AI summarizes **only** what Priya actually recorded — never extrapolates clinical opinion.
- Everything is review-then-submit; nothing auto-submits under Priya's name.
- One clear notification per obligation, not per system event.

## Language & Accessibility

- English; clinical register in professional output
- Voice input is a genuine unlock given mobility and time pressure
- Must work on a small screen and a poor school network

## Anti-Goals — what makes Priya disengage

- **Another login and another password.** SSO or a magic link, or Priya will not come.
- **Being asked to learn the whole product** to make a small contribution.
- **Notification spam** from many case managers — Priya will mute the channel, and then everything breaks.
- **Duplicate entry** across session notes, progress reports, and IEP input.
- **Being blocked** by a permission or workflow state at the moment they finally have two minutes.

## Design Implications

1. **Task-first entry, not app-first.** Priya's canonical entry is a notification or link that lands **directly on the one thing they owe**, with context ("progress on Goal 2 for Jordan, due Friday"). Landing on a dashboard is a failure.
2. **Consider passwordless/magic-link or SSO for this persona specifically.** The login step is a larger obstacle than the task.
3. **A 60-second session-note capture** — student, date, what happened, data point — optimized for immediately-after-session use. Voice-capable. Mobile-first. This is the only mobile-first school-side surface.
4. **Session notes compose upward.** Notes → progress report → present levels → annual review, with no re-entry. Write once, reused three times. This is Priya's biggest possible win.
5. **A cross-caseload "what I owe" queue** spanning all case managers and buildings — the view Priya's actual job has and no current system provides.
6. **Notification consolidation.** One digest, batched, per person — not one per request per case manager.
7. **Partial contribution is a first-class state.** Priya can submit input for one goal without touching the rest of the IEP.
8. **Attribution is preserved.** Priya's words appear as Priya's, sourced, not folded anonymously into Steph's prose.
9. **Off-track alerts to Priya, not just to Steph** — Priya is the one who can change the intervention.

## Evidence vs. Assumption

| Claim | Status | How to validate |
|---|---|---|
| Progress data is chronically late and recall-based | **Assumption** (widely reported) | Compare session dates to entry timestamps in pilot |
| Login friction is the dominant barrier for this persona | **Assumption** | Measure invite-to-first-contribution conversion |
| Mobile capture would be used immediately post-session | **Assumption** | Pilot a mobile capture surface; measure entry latency |
| Notification volume across many case managers causes muting | **Assumption** | Track notification settings changes in pilot |
| Service providers span multiple buildings and case managers | **Assumption** (common but district-dependent) | Pilot district staffing data |
| Product today models assigned staff per student | **Evidence** (staff invites, assigned-staff panel, roster scoping) | — |
| Product today has no progress-report authoring by service providers | **Evidence** (progress reports are parent-side viewers) | — |
| Product today has no session-note concept | **Evidence** | — |

## Open Questions

- Is the general-education teacher the same persona as the SLP/OT, or a distinct one? (They contribute differently: classroom observation vs. clinical service data, and they're not on a caseload.)
- Does Priya need a full account, or is a scoped contribution link sufficient? A lighter identity model may be the right answer.
- Service-minute tracking is a compliance obligation in many districts — is that in our scope or the SIS's?
- Do we ever need to show Priya the whole IEP, or is their view permanently scoped to their sections?
