# P7 — Karen, District Special Education Director

**Type:** Primary (economic buyer)
**Status:** Draft — unvalidated
**Journeys:** [J2 School onboarding](../journeys/J2-school-onboarding.md) ◆ · [J3 ETR](../journeys/J3-etr-eligibility.md) · [J5 School-only](../journeys/J5-school-only-fallback.md) · [J7 Progress](../journeys/J7-progress-monitoring.md) · [J8 Annual review](../journeys/J8-annual-review.md)

> "I can defend a decision we made carefully and documented well. I cannot defend not knowing it happened."

## Snapshot

| | |
|---|---|
| **Who** | Director of Student Services for a district of ~6,000 students, ~800 on IEPs, 6 buildings, ~30 special-education staff |
| **Relationship to product** | **The buyer.** Signs the contract, owns the rollout, answers to the superintendent, the school board, the state, and occasionally a hearing officer |
| **Accountability** | State performance-plan indicators, timeline compliance, disproportionality, due-process defense, staff retention, budget |
| **Tech comfort** | Moderate. Comfortable with reports and dashboards; not a hands-on user of authoring tools |
| **Time budget** | Scans weekly. Deep-dives when something is wrong or a deadline approaches |
| **Device** | Laptop; phone for email |
| **Language** | English |
| **Emotional baseline** | Risk-managing. Genuinely mission-driven and structurally forced to prioritize defensibility |

## What Karen is actually buying

Not a better editor. Karen buys **three things**, in this order:

1. **Reduced risk** — fewer timeline misses, fewer procedural defects, fewer due-process filings, a clean state monitoring visit.
2. **Retained staff** — special-education staffing is a crisis and paperwork burden is a leading cause of attrition. A tool that gives Steph (P4) hours back is a retention intervention with a budget line.
3. **Defensible family engagement** — documented, meaningful parent participation is both legally required and the strongest predictor of *not* ending up in a hearing.

Notice what's absent: features. Karen will never evaluate the goal editor. Karen will ask what happens when the state audits us, what happens to the data, and whether the staff will actually use it.

Karen also holds the **veto**. Any product behavior that increases legal exposure — discoverable AI commentary, parent-visible drafts, family risk-scoring — is a procurement blocker regardless of how much Steph and Dana like it.

## Jobs to be Done

1. *"Show me where we're out of compliance before the state does."*
2. *"Tell me if a family relationship is deteriorating while I can still fix it."*
3. *"Give me the evidence for a monitoring visit or a hearing without a two-week scramble."*
4. *"Show me where the workload is unevenly distributed, so I can staff to it."*
5. *"Show me whether the plans we write are actually working"* — outcomes, not just documents.
6. *"Prove this tool is safe"* — FERPA, data ownership, AI governance, vendor risk.
7. *"Get my staff to adopt it"* — a bought-and-unused tool is worse than no tool.

## Goals

- Zero timeline violations and a clean state monitoring cycle
- Fewer due-process filings and facilitated-IEP escalations
- Measurable reduction in staff paperwork hours
- Documented, meaningful parent participation across the district — including families who don't speak English
- A defensible record for any student, retrievable in minutes

## Pains & Frictions

- **Compliance is discovered late.** Karen finds out about a missed timeline when it's already missed.
- **Data assembly is manual.** Every report request means asking building administrators to compile spreadsheets.
- **Quality varies invisibly.** Karen has no way to know that one building's IEPs are boilerplate until something goes wrong.
- **Due process is a black swan.** Rare, enormously expensive, and preceded by warning signs nobody was watching.
- **Staff turnover resets institutional knowledge** and creates coverage gaps mid-year.
- **Family engagement is unmeasured** — and unmeasured engagement is undefendable engagement.
- **Vendor sprawl and procurement pain.** Karen has an SIS, an IEP system, an assessment platform, and a parent portal. A new tool must justify existing beside them or replace one.
- **Language access is a known, chronic gap** with real legal exposure and no good current answer.

## Context of Use

- **Weekly scan**, exception-driven: what's off-track, what's overdue, what's escalating
- **Quarterly and annual reporting** cycles requiring extraction
- **Incident-driven deep dives** — a complaint, a filing, a monitoring notice
- **Procurement and renewal**, where security review and adoption metrics decide everything

## AI Trust Posture

Karen is the **most AI-cautious persona** and the one whose approval AI features must pass.

| Wants | Won't tolerate |
|---|---|
| AI that reduces staff burden — measurably | Student data used to train external models |
| Compliance and timeline detection | AI-generated content entering legal documents without human approval |
| Language access at a quality that reduces exposure | Anything that creates discoverable commentary about families or staff |
| A written, auditable AI governance story | "The AI decided" as an answer to any question |

**Trust rules:**
- **Data handling must be documentable**: where it's stored, who can see it, retention, deletion, whether it trains anything. Karen's security review will ask in writing.
- **Human-in-the-loop must be provable**, with an audit trail showing who approved what.
- **Nothing is auto-shared with families.** Every family-visible item has a human sender.
- **AI output is never characterized as a legal determination.**

## Language & Accessibility

- English; buys language access **for the district's obligation**, not for personal use
- Needs evidence of translation quality and clarity on whether it satisfies or merely supplements the district's interpreter/translation duties (see P2) — this will be asked in procurement
- District-wide accessibility conformance (WCAG) is a procurement requirement

## Anti-Goals — what makes Karen say no

- **Any increase in legal exposure.** The single biggest veto.
- **Data-handling ambiguity.** An unclear FERPA answer ends the conversation.
- **AI writing into legal documents unsupervised.**
- **Family risk-scoring or staff productivity ranking** — both are discoverable and both damage trust.
- **Low adoption.** A tool the staff bypasses is a budget line Karen has to defend for nothing.
- **Parent-facing features that create obligations the district didn't agree to** — e.g. an expectation of real-time draft access that becomes a demand.

## Design Implications

1. **A district oversight surface that is exception-driven, not exploratory.** Karen's home is "what needs attention," not a data warehouse. Overdue, at-risk, unassigned, unresponsive.
2. **Compliance timelines are a first-class domain object** — annual review dates, three-year re-evaluations, evaluation-to-eligibility clocks, invitation notice — with forward-looking alerts at district, building, and staff level. This is the product's clearest institutional value and does not exist today.
3. **Audit-log and evidence export are sales features, not admin features.** "Produce the complete record for this student" in one action. The existing `AccessAuditLog` and audit viewer are the seed of this.
4. **Family-engagement metrics as a defensibility artifact**, framed as *evidence of meaningful participation* — input solicited, input received, documents shared, languages served. Framed as surveillance, it's a liability; framed as evidence, it's a selling point.
5. **AI governance is a documented product surface**, not a policy PDF: what AI touched, what a human approved, what data was used. Karen's security reviewer needs to see it.
6. **Workload distribution, not individual productivity.** Show caseload sizes and due-date pileups by building and role. **Never** rank individuals — that's a union problem and a trust problem.
7. **Rollout tooling matters as much as features.** Karen owns adoption; the product must show adoption per building and make onboarding staff cheap. The existing district setup wizard and staff invites are the foundation.
8. **Mode B must be complete and exportable**, because Karen must guarantee the district can produce a compliant document regardless of family participation — and must be able to leave the platform with their data.
9. **Boundary honesty.** Be explicit about what the product is *not* (not a legal advisor, not a substitute for an interpreter, not a decision-maker). Karen trusts a vendor who states limits — and as system of record we are claiming a great deal, so the limits we do state carry more weight.

## Evidence vs. Assumption

| Claim | Status | How to validate |
|---|---|---|
| Compliance risk and staff retention outrank features in the buying decision | **Assumption** (consistent with edtech procurement patterns) | Structured buyer interviews |
| Karen holds veto power over parent-facing and AI features | **Assumption** | Observe pilot procurement conversations |
| Timeline compliance tracking is the top unmet need | **Assumption** | Rank-order with several directors |
| Paperwork burden is a leading driver of special-ed attrition | **Evidence** (well documented in workforce research) | — |
| Documented parent participation reduces due-process risk | **Evidence** (established in special-education law/practice) | — |
| Data-handling questions gate procurement | **Assumption** (strongly expected) | First security review will confirm |
| Product today has org signup, schools, staff invites, and an audit log viewer | **Evidence** (`district-admin`, `staff-invites` features) | — |
| Product today has no compliance-timeline tracking or evidence export | **Evidence** | — |

## Resolved Decisions

- **We are the system of record**, replacing the district's IEP software rather than sitting beside it. *(2026-08-03.)* This changes Karen's buying decision materially: it's a **displacement sale** against an incumbent with switching costs, migration risk, and an existing contract — not an additive purchase. It raises the security-review bar, makes data portability a contractual requirement rather than a nicety, and means an outage is a compliance emergency. It also removes the "one more system" objection entirely and makes the value case much larger.

## Open Questions

- Is Karen the buyer, or is it the superintendent / curriculum director / IT? Who actually signs, and who can veto?
- **What's the incumbent, and what does displacing it cost?** Migration of existing IEPs, contract timing, and staff retraining are now central to the sale.
- What's the pricing model — per student, per staff seat, per district? It changes which adoption metrics matter.
- Do we need a formal AI governance / data-processing agreement artifact before the first real district contract? (Almost certainly yes.)
- How do we handle a district that wants to disable parent-facing collaboration entirely?
