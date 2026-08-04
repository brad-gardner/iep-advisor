# Platform Feature Expansion Brainstorm

**Date:** 2026-03-14
**Status:** Reviewed
**Context:** The IEP Assistant currently reads IEP PDFs, extracts sections/goals, and runs AI-powered analysis (SMART criteria, red flags, suggested questions). This brainstorm explores what additional features would make it a complete platform parents rely on.

---

## What We're Building

A comprehensive IEP advocacy platform that goes beyond document analysis to help parents at every stage: understanding the IEP, defining their own priorities, preparing for meetings, tracking changes over time, and securely sharing with their support team.

**Target users:** Parents at any stage of the IEP journey — from first-timers who need education and hand-holding, to experienced advocates who want accountability tools.

**Business model direction:** Annual subscription with document/analysis usage limits (AI cost-driven). Read-only access to existing data is preserved indefinitely even if subscription lapses.

---

## Feature Tiers

### Tier 1: Core Differentiators

#### 1. Parent Goal Advocacy System
Parents define goals and priorities for their child (e.g., "improve reading fluency," "more time in general education," "speech therapy 3x/week"). When an IEP is analyzed, the AI cross-references these parent goals against the IEP's official goals and flags:
- "Your priority X is not addressed in this IEP"
- "Goal #3 partially addresses your concern about Y"
- "The services section does not include the speech therapy frequency you want"

**Why this approach:** This is the killer differentiator. No other tool lets parents define what *they* want and then checks whether the IEP delivers. It transforms the platform from passive analysis to active advocacy.

**Key decisions:**
- Parent goals are stored in a new `ParentGoals` table with FK to `ChildProfile` (goalText, category tag, priority, isActive, audit fields)
- Goals are free-text with optional category tags (academic, behavioral, services, placement)
- Analysis prompt is augmented with parent goals when they exist
- Gap analysis is a dedicated section in the analysis output
- Analyses are immutable snapshots — if parent goals change after analysis, show a banner: "Your goals have changed since this analysis. Would you like to re-analyze?" Parent can manually re-trigger.

#### 2. IEP as an Event (Enhanced Upload Flow)
Currently IEP upload is just a file drop. Instead, parents should create an IEP record with metadata, then attach the document:
- IEP meeting date
- Meeting type (annual review, initial, amendment, reevaluation)
- Attendees (optional, free-text list)
- Notes from the meeting (optional)
- File attachment (PDF upload)

**Why this approach:** Treating the IEP as an event rather than just a file creates the foundation for timeline views, deadline tracking, version comparison, and meeting prep. The metadata makes the platform smarter without relying solely on AI extraction.

#### 3. IEP Version Comparison
Parents upload multiple IEPs over time (annual reviews, amendments). The system provides:
- Structural diff of parsed Goal entities (field-by-field comparison)
- Service changes: hours increased/decreased, providers changed
- Timeline view per child showing IEP evolution (using event metadata from Feature 2)
- Optional LLM summary of the most significant changes
- Whether red flags from previous analysis were addressed

**Why this approach:** IEPs change yearly and parents often can't tell what shifted. Automated diffing surfaces changes they'd otherwise miss, especially subtle reductions in services.

#### 4. Meeting Prep Checklists
AI-generated, actionable checklists based on the current IEP analysis:
- Questions to ask (prioritized by red flag severity)
- Documents/records to bring or request
- Red flags to raise with specific talking points
- Rights to reference (with plain-language explanations)
- Parent goal gaps to advocate for (if parent goals exist; omitted otherwise)
- Exportable/printable format (PDF)

Checklists are persisted — parents can check off items and revisit. Can regenerate, which creates a new version.

**Why this approach:** Checklists are higher priority than email templates. Parents need to walk into meetings prepared and confident. This is the bridge between "understanding" and "action."

### Tier 2: Platform Maturity

#### 5. Security Hardening
- **MFA:** TOTP-based (Google Authenticator, Authy) — implemented with minimal external dependencies (custom TOTP generation, not a heavy auth library)
- **MFA recovery:** Generate one-time recovery codes at enrollment, displayed once for the parent to save
- **Password reset:** Email-based reset flow
- **Session management:** View/revoke active sessions, configurable session timeout
- **FERPA compliance posture:** Data handling documentation, Azure-managed encryption at rest, audit logging of access events
- **Account deletion:** Data export (JSON + original PDFs as ZIP) and deletion

**Why this matters:** Parents are sharing sensitive child disability and education data. Security is a trust requirement, not a nice-to-have.

**Implementation principle:** Minimize external library dependencies. Prefer simple, custom implementations where reasonable (e.g., TOTP generation is straightforward to implement directly).

#### 6. Guided Onboarding
- Step-by-step first-time flow: "What is an IEP?" context, how to use the platform, what to expect from analysis
- Contextual tooltips and educational content woven into the experience
- "IEP 101" reference section for parents new to special education
- Progressive disclosure — track onboarding completion, allow skip and replay
- Ensure jurisdiction (state) is captured during onboarding for state-specific legal content later

#### 7. Shared Access
- Invite co-parent, advocate, or attorney to view a specific child's profile and IEP analysis
- Role-based access: Viewer (read-only: child profile, IEPs, analyses, parent goals) vs. Collaborator (can add notes/goals)
- Invitation via email link with expiry, account creation for new users
- Per-child sharing (not account-wide)
- Primary parent controls all sharing and can revoke at any time
- Contributions from revoked collaborators remain visible to the primary parent

**Deferred:** Custody-aware access control and child identity deduplication across accounts — complex and edge-case-heavy, handle later.

### Tier 3: Business & Growth

#### 8. Subscription & Usage Limits
- Annual subscription model with document/analysis quotas (per-account)
- Usage tracking dashboard for the parent
- Billing integration (Stripe)
- Free tier consideration: 1 document analysis to demonstrate value
- **Lapsed subscriptions:** Read-only access to all existing data preserved indefinitely. Only new uploads, analyses, and AI-powered features are gated.
- Grace period behavior: existing checklists, analyses, and documents remain fully accessible

#### 9. Knowledge Base / Legal Reference
- Federal IDEA provisions in plain language
- State-specific legal content (starting with pilot states)
- Searchable glossary of IEP/special education terms
- "Know your rights" section with actionable guidance
- RAG-powered contextual legal references during analysis (already in PLAN.md Phase 5-6)
- Content maintenance: manual curation with version tracking, legal disclaimer on all content

#### 10. Progress Monitoring & Deadline Tracking (Lower Priority)
- Calculate key deadlines from parsed IEP data and event metadata (annual review due date, reevaluation every 3 years)
- Let parents log when progress reports are received
- Flag overdue progress reports based on the IEP's stated reporting schedule
- Simple timeline/calendar of upcoming IEP-related dates per child
- Depends on Feature 2 (IEP as an Event) for meeting date metadata

#### 11. Notification System (Lower Priority)
- Email notifications for key events: "Analysis ready," "Annual review due in 30 days," "Collaborator added a note," "Progress report may be overdue"
- In-app notification center
- User-configurable notification preferences
- Makes the platform proactive rather than pull-based
- Depends on Features 10 (deadlines) and 7 (shared access) for notification triggers

#### 12. Communication Templates (Lowest Priority)
- Meeting request emails
- Disagreement/concern letters
- Progress report request templates
- Consent/refusal letters
- LLM-assisted drafting personalized to the child's situation
- Export to email/PDF
- Parent copy-pastes output into their own email client (platform does not send emails on behalf of parents)

---

## Key Decisions

1. **Parent goals are the core differentiator** — they feed into analysis and make the platform active, not passive
2. **IEP is an event, not just a file** — capturing meeting date, type, and attendees enables timeline, comparison, and deadline features
3. **Analyses are immutable snapshots** — if parent goals change, prompt to re-analyze rather than auto-updating
4. **Checklists over templates** — meeting prep checklists are higher priority than email/letter generation
5. **Security is Tier 2 but non-negotiable** — MFA, password reset, and FERPA posture are required before public launch
6. **Minimal external dependencies** — prefer simple custom implementations (e.g., TOTP) over heavy libraries
7. **Shared access is per-child** — co-parents and advocates get scoped access, not full account access
8. **Annual subscription with usage limits** — driven by AI costs; read-only access preserved forever even if lapsed
9. **Serve both new and experienced parents** — onboarding for newcomers, power features for advocates

## Resolved Questions

- **Goal change after analysis:** Show banner + prompt to re-analyze (snapshot model)
- **Subscription lapse:** Read-only access forever; only new AI operations gated
- **MFA recovery:** One-time recovery codes at enrollment
- **IEP comparison approach:** Structural diff of parsed entities + optional LLM summary
- **Checklist persistence:** Persisted with check-off state; regenerate creates new version
- **Shared access data scope:** Viewers see child profile, IEPs, analyses, parent goals; not communication drafts
- **Custody conflicts:** Deferred; each parent has independent profiles, sharing is opt-in
- **Non-IEP document uploads:** Claude parsing includes validation; non-IEP docs get error status with explanation
- **Parent goal data model:** New `ParentGoals` table with FK to ChildProfile

---

## Cross-Feature Dependencies

```
Feature 1 (Parent Goals) ← standalone, build first
Feature 2 (IEP as Event) ← standalone, enhances upload flow
Feature 3 (Version Comparison) ← depends on Feature 2 (event metadata)
Feature 4 (Meeting Prep) ← enhanced by Feature 1 (parent goals), works without it
Feature 5 (Security) ← standalone, prerequisite for Feature 7 (Shared Access)
Feature 6 (Onboarding) ← standalone, ensures state is set for Feature 9 (Knowledge Base)
Feature 7 (Shared Access) ← depends on Feature 5 (Security)
Feature 8 (Subscription) ← standalone
Feature 9 (Knowledge Base) ← enhanced by Feature 6 (state capture)
Feature 10 (Deadlines) ← depends on Feature 2 (event metadata)
Feature 11 (Notifications) ← depends on Features 7 + 10
Feature 12 (Comms Templates) ← depends on Feature 9 (Knowledge Base)
```

## Recommended Build Order

1. **Parent Goal Advocacy System** (Tier 1 — highest impact, no dependencies)
2. **IEP as Event** (Tier 1 — refactor upload flow, foundation for timeline features)
3. **Security Hardening** (Tier 2 — required for trust/launch)
4. **Meeting Prep Checklists** (Tier 1 — bridges understanding to action)
5. **IEP Version Comparison** (Tier 1 — high value, depends on Event model)
6. **Guided Onboarding** (Tier 2 — improves new user experience)
7. **Shared Access** (Tier 2 — requires security first)
8. **Subscription & Usage Limits** (Tier 3 — needed for launch)
9. **Knowledge Base** (Tier 3 — enriches analysis and comms)
10. **Progress Monitoring & Deadlines** (Tier 3 — extends event model)
11. **Notifications** (Tier 3 — makes platform proactive)
12. **Communication Templates** (Tier 3 — lowest priority)
