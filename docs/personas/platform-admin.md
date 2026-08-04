# P8 — Sam, Platform Admin (internal)

**Type:** Internal
**Status:** Draft — unvalidated
**Journeys:** [J1 Parent-only adoption](../journeys/J1-parent-only-adoption.md) · [J2 School onboarding](../journeys/J2-school-onboarding.md)

> "Someone's locked out of their kid's IEP two days before the meeting. I need to fix that in five minutes without ever reading the document."

## Snapshot

| | |
|---|---|
| **Who** | Our own operations/support person. Wears support, trust-and-safety, and onboarding hats |
| **Relationship to product** | **Operator, not user.** Serves the other seven personas and never participates in an IEP |
| **Volume** | Small now; grows with every district. Early on this is a founder or a single ops hire |
| **Tech comfort** | Highest. Will use SQL if the admin surface fails them — which is precisely the outcome to design against |
| **Time budget** | Interrupt-driven. Every task is someone else's emergency |
| **Device** | Laptop |
| **Emotional baseline** | Wants to help fast and is acutely aware of handling other people's children's records |

## The defining tension

Sam needs enough access to resolve problems, and **must not have casual access to student educational records.**

Every other persona's product experience is about capability. Sam's is about **bounded capability with a trail**. FERPA exposure via an over-powered internal admin panel is a genuine way to lose a district — and it's the kind of thing Karen (P7) asks about during security review.

The design principle: **Sam can see structure, not content.** Sam should be able to see that a document exists, who can access it, and that processing failed — without reading the student's present levels.

## Jobs to be Done

1. *"Get this person into the account they should have access to, quickly."*
2. *"Tell me what actually went wrong for this user, without reading their child's records."*
3. *"Fix a broken invite/link/role without a database console."*
4. *"Get a new district onboarded and configured correctly."*
5. *"Show me what's failing across the platform right now"* — processing failures, bounced email, stuck jobs.
6. *"Handle a data request"* — export or delete a user's data, defensibly.
7. *"Prove what I did"* — every administrative action attributable and reviewable.

## Goals

- Resolve support issues in minutes without engineering escalation
- Never read student content unnecessarily — and be able to prove it
- Onboard a district without a manual runbook
- Catch systemic failures before users report them
- Keep the internal surface boring, safe, and reversible

## Pains & Frictions

- **Escalation to engineering** for anything the admin panel doesn't cover — slow for the user, expensive for us.
- **Debugging blind.** A user says "it didn't work"; Sam has no view of that user's actual state.
- **Silent failures.** A document that fails to parse, an invite email that bounces — the user experiences a dead end and may never report it.
- **Identity and role tangles.** Wrong role, wrong org, an invite accepted by the wrong account, a parent whose email changed. These are the modal support tickets and they are all permission-graph problems.
- **All-or-nothing access.** Most internal tools force full record access to do routine work.
- **No undo.** Destructive admin actions with no reversal path make Sam hesitant exactly when speed matters.

## Context of Use

- Interrupt-driven, in response to a support ticket or an alert
- Frequently under time pressure tied to someone else's meeting date
- Often working from a partial description ("my link doesn't work")
- Occasionally during onboarding, in a sustained configuration session

## AI Trust Posture

Modest and operational.

| Wants | Won't tolerate |
|---|---|
| Summarize what happened to this user from logs and events | AI reading student document content to answer support questions |
| Cluster incoming issues to surface systemic problems | Automated destructive actions |
| Draft support replies | Anything that bypasses the audit trail |

**Trust rule:** AI assists diagnosis from **system events**, never from educational records.

## Language & Accessibility

- English internal tooling
- Must be able to help users in other languages — see the *content* a user sees without needing to read student data (e.g. render the UI in Spanish to reproduce a bug)

## Anti-Goals

- **Being forced into the database** to do routine work — every occurrence is a design failure and an audit gap
- **Unlogged access** to student records
- **Irreversible actions** without confirmation and a trail
- **Having to ask engineering** for common operations

## Design Implications

1. **Structural visibility, content restriction.** Sam sees users, orgs, roles, permission grants, document metadata, processing status, and events — **not document content**. Content access is a separate, justified, explicitly logged, time-boxed escalation.
2. **The permission graph is the primary support object.** Most tickets are "who can see what and why." A view showing, for a given student, every principal with access and the grant that produced it would resolve the majority of cases directly.
3. **Impersonation must exist and must be conspicuous.** Support cannot function without seeing what the user sees. It must be consent-or-policy-gated, banner-visible, time-limited, fully logged, and ideally content-redacted by default.
4. **Every administrative action is logged with actor, target, reason, and timestamp** — the same audit rigor the product sells to Karen (P7) applies internally, and Karen will ask.
5. **A platform health view**: failed document processing, bounced invitations, stuck background jobs, expiring invites. Sam should find these before users report them.
6. **District onboarding is a guided internal flow**, not a runbook — the manual steps around the existing self-serve signup are where configuration errors originate.
7. **Data subject requests are a supported operation** — export and delete, scoped and logged. FERPA and state privacy laws make this table stakes, and it should exist before the first district contract, not after the first request.
8. **Reversibility by default.** Soft-delete, undo windows, and confirmation on anything destructive.

## Evidence vs. Assumption

| Claim | Status | How to validate |
|---|---|---|
| Access/permission tangles are the modal support ticket | **Assumption** | Categorize tickets once volume exists |
| Support can operate without reading student content | **Assumption — worth defending hard** | Track how often content escalation is genuinely needed |
| Silent processing failures create unreported dead ends | **Partial evidence** (upload → parse pipeline has an `error` status; no alerting) | Instrument failure rates |
| Internal admin access will be a procurement question | **Assumption** (strongly expected) | First district security review |
| Product today has an admin dashboard and user management | **Evidence** (`/admin`, `/admin/users`, `AdminRouteGuard`) | — |
| Product today has an append-only access audit log | **Evidence** (`AccessAuditLog`, `AccessAuditLogWorker`) | — |
| Product today has no impersonation, health view, or DSR tooling | **Evidence** | — |

## Open Questions

- Does impersonation require explicit user consent, district policy consent, or is a logged banner sufficient? This needs a decision before it's built.
- What's the retention policy for student records after a district leaves, and who executes deletion?
- At what scale does Sam split into distinct support and trust-and-safety roles?
- Do districts get their **own** admin visibility into our internal access to their data? (Karen would value it; it's a differentiator.)
