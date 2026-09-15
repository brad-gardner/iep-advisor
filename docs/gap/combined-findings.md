# School demo and sales readiness — combined findings

**Date:** September 15, 2026  
**Audited baseline:** `590badd`  
**Sources:** [Claude findings](claude-findings.md) and [Codex findings](codex-findings.md)  
**Purpose:** One consolidated, prioritized review of what it will take to demonstrate and sell the platform to a school. Overlapping findings are merged; differing observations and recommendations are reconciled below. This is a synthesis of the two audits, not a new live walkthrough or compliance certification.

## 1. Executive assessment

**The product has substantial foundations, but it does not yet deliver the complete school workflow described in the personas and journeys.** The biggest investment is connecting existing capabilities, then filling the missing roster, team, scheduling and record-management workflows.

Three findings drive the plan:

1. **The default template editor is disconnected from existing AI, student input and parent viewing.** The stronger legacy editing tools exist, but a customer following normal navigation does not encounter them. Parents cannot use the normal parent interface to review the template editor's output, and parent AI analysis only accepts uploaded documents.
2. **There is no operational model of meetings, deadlines and work due.** Current dashboards report setup and roster health. Useful alerts, calendars and oversight require real dates, obligations, ownership and workflow events first.
3. **Student teams are modeled as permission grants, and the roster is incomplete.** Schools can create students and grant access, but cannot perform the requested repeatable XLSX import or manage a complete student/team lifecycle.

The [persona](../personas/00-persona-index.md) and [journey](../journeys/00-journey-index.md) indices commit to being the **IEP/ETR system of record**. Therefore, reviewed state-form output, signed records, amendments, durable evidence, reliable operations and district exit/export are part of the commercial product—not optional administrative polish.

### Recommended product priorities

| Order | Outcome | Why it matters |
|---|---|---|
| 1 | One authoring workflow with student evidence, structured goals, AI and parent document access | Repairs the most important disconnected capabilities. |
| 2 | Complete roster management, safe XLSX re-import and intuitive IEP team assembly | Makes school onboarding practical. |
| 3 | Meetings, deadlines and task records, followed by role-specific dashboards | Makes the product useful every day and gives the buyer meaningful oversight. |
| 4 | Whole-draft parent sharing, private AI understanding and submitted questions | Completes the collaborative story requested for the demo. |
| 5 | Verified launch-state forms and supported meeting-to-final-record lifecycle | Makes output credible and usable, including school-only operation. |
| 6 | Operational, security, procurement and release evidence | Qualifies a bounded real-data paid pilot. |

Some work can proceed in parallel after agreeing shared models. **The integrated demo must include parent review and reviewed form output**, regardless of implementation order.

## 2. Current coverage of the seven school goals

| Requested goal | Combined assessment | Principal gap |
|---|---|---|
| Manage users/roles and form an IEP team | **Partial** | Organization roles and student access exist; functional team roles, case manager ownership, cross-school providers and staff lifecycle are incomplete. |
| Manage students and re-upload XLSX roster | **Creation/viewing exist; import missing** | Edit/archive/transfer, stable district student ID, validation, preview, repeatable updates and history. |
| Professional dashboard with alerts/analytics | **Consistent visual foundation; thin operational content** | Case-manager work queue, upcoming meetings, deadlines, review status and actionable drilldowns. |
| IEP scheduling and staff calendar | **Operational workflow missing** | Meeting/participant objects, invitation lifecycle, calendar, reminders, attendance and rescheduling. |
| IEP/ETR drafting, state forms and contextual AI | **Substantial but disconnected** | AI/student input in the active editor, populated drafts, durable goal identity and verified state packages. Claude's QA walkthrough found ETR/504 creation blocked by missing templates. |
| Send draft to parents for review and AI help | **Missing in the active workflow** | Parent UI for template documents, deliberate draft snapshots, shared-revision AI sources, private preparation and submitted responses. |
| Shared student goals/bio used by AI | **Fragmented partial capabilities** | Attributable shared profile, permission-filtered evidence, conflict resolution and goals that persist across revisions/years. |

## 3. Evidence, confidence and differences between the audits

### What was verified

| Evidence | Result and limitation |
|---|---|
| Code and product-document review | Both audits reviewed the same baseline and independently reached the principal findings above. |
| Backend tests | Both report **485/485 passing**. Codex recorded one nullable-reference warning in `IepProcessingService.cs:57`. |
| TypeScript and UI guard | Codex ran `npx tsc -b` and `npm run guard:ux`: **passed**. The guard checks selected conventions, not visual quality or accessibility conformance. |
| Frontend build/unit tests | Codex encountered a local esbuild mismatch: host `0.28.1`, binary `0.25.11`. Build and tests were blocked at startup. This is an installation issue observed in that environment, not proof of a source defect. |
| Live walkthrough | **Claude reports** walking district signup → setup wizard → roster → student → document editor against QA. Codex did not perform a live walkthrough. These statements can both be true; neither establishes a successful full cross-role school journey. |
| Production readiness | Neither audit establishes production configuration, delivered notifications, tested restoration, contractual readiness or comprehensive tenant isolation. |

### Resolutions used in this combined report

| Difference | Consolidated treatment |
|---|---|
| Ohio is assumed by Claude; Codex leaves the state open | **Recommend evaluating Ohio first, but keep launch-state selection an explicit business decision.** An OH walkthrough and ETR terminology do not establish user approval of Ohio-only scope. |
| Claude recommends new Goal/Service/Accommodation field types | Preserve this as a strong **architecture proposal**, not a settled implementation requirement. Stable semantic identities and reusable editing/AI interfaces are required; typed blocks or separately stored entities can satisfy them. |
| Claude says school roles share one home | Admin and teacher homes already differ. The valid gap is useful case-manager/provider/LEA experiences and functional team responsibilities. |
| Team role should derive permission versus roles and permissions kept separate | Team roles may supply defaults, but **responsibility and permission remain distinct**. Being an LEA representative or provider should not automatically imply broad editing/admin access. |
| Broad “no time model” claims | Parent meeting-prep dates and staff-invite expiry/reminders exist. Student procedural timelines and operational meetings/calendars do not. |
| Claude proposes typed-name signature capture | Do not equate a typed name/IP/timestamp or Finalize click with a sufficient signature process. Agree and verify the supported signing procedure; print/sign plus signed-artifact retention may suit a bounded pilot. |
| Claude suggests demo readiness after Phase 3 | Its parent loop and full form work land later. **Require the complete requested story before calling the integrated demo ready.** Earlier phases support narrower capability demos only. |
| Claude provides person-week estimates and small “one-day” fixes | Treat them as unvalidated estimates. No delivery commitment is adopted here; scope the shared models, migration and acceptance tests first. |
| Language, SSO, 504 and district customization timing | Determine from the first buyer's needs. Participating families' accessibility/language requirements cannot simply wait; broad language coverage, 504 and customization are not automatically on the demo critical path. |
| PDF approximation versus state-form fidelity | Recognizable styling is insufficient. Have the supported output reviewed against the actual applicable forms and procedures. |

The Codex audit checked Ohio's official forms page, which announced a transition to revised forms for 2027–28, with the updated forms not required during 2026–27. If Ohio is selected, pin and manage form versions from the start. [Ohio Department of Education and Workforce — Required and Optional Forms](https://education.ohio.gov/Topics/Special-Education/Federal-and-State-Requirements/Ohio-Required-and-Optional-Forms-Updated) (source check recorded September 15, 2026).

## 4. Foundations to keep

- **Organization and identity:** district signup/setup, schools CRUD, staff invitations/resend/revoke/expiry reminders, staff deactivation/reactivation, scoped rosters, JWT authentication, MFA, lockouts and security-stamp revocation.
- **Authoring infrastructure:** state/type template resolution, published-version pinning, concurrency handling, autosave, finalized snapshots, PDF rendering and retry. Legacy IEP editing adds structured goals/services, lineage, AI assist/chat and student-input controls.
- **Parent capabilities:** profiles, IEP/ETR/progress documents, parsing and analysis, advocacy goals, meeting preparation, document comparison, sharing with another adult, knowledge base and IEP 101.
- **Student voice:** strengths, interests, accommodation requests, meeting statements and interview entries with explicit shareability. This should be integrated, not rebuilt.
- **UI and administration:** reusable design-system components, district tiles, audit viewer, platform user management and template builder.

Claude describes the live UI as visually consistent and professional in its basic typography/colors. The principal dashboard problem is information and workflow design; it does not require discarding the design system.

## 5. Consolidated gap backlog

**Gates:** **D** = required for the integrated school demo; **P** = required before the supported real-data pilot; **E** = expansion unless the buyer makes it a prerequisite. Items spanning gates distinguish the demo slice from full operational scope.

### C01 — Unify authoring and preserve structured goal identity

**Gate: D · Source: both; typed-block proposal and live editor observations from Claude**

`web/src/config/features.ts` selects the template editor. `document-authoring/components/document-editor.tsx` renders fields/autosave/finalization without the legacy assist/chat or pull-from-student tools. `DefaultIepTemplateSeeder.cs` represents goals as a generic table; legacy `IepDraftGoal` has a `LineageId` that the new goal representation does not preserve as a semantic goal identity.

**Required outcome:**

- One normal Documents entry point and authoring experience, with section navigation, clear save/conflict state, AI help and approved student input.
- Stable identities for goals and relevant service/accommodation records so comments, AI, progress and amendments can address them across revisions.
- Port capabilities into reusable, small React components. Evaluate typed blocks versus a template-to-domain-entity mapping before committing the storage design.
- Migrate existing template values deliberately; preserve old template/version rendering and legacy document history. Retire legacy editing routes only after parity, migration and access to historical records are verified.

**Acceptance:** a case manager uses ordinary navigation to edit a real template goal, request/accept assistance, pull shared student input, save, finalize and retrieve the document. No feature-flag switch or hidden legacy route is necessary.

### C02 — Start populated and ground AI in authorized student evidence

**Gate: D · Source: both**

`DocumentInstanceService.cs` creates empty `ValuesJson`. `IepAssistService.cs` supplies current draft/field text, not a comprehensive evidence record. Existing human accept/dismiss controls are worth keeping.

**Required outcome:**

- Pre-fill verified demographics and team information; propose prior IEP/ETR/progress content with source and date labels. Flag stale carry-forward instead of silently repeating old goals.
- Build a queryable, revisioned evidence bundle from authorized profile facts, assessments, prior plans, progress, and explicitly shared family/student contributions.
- Expose sources alongside the field. AI suggestions reference exact source revisions and require human acceptance; capture the actor, model and evidence used.
- Treat unknown baselines and conflicting evidence explicitly. Do not invent student scores, observed progress or existing service commitments.
- Keep private parent preparation and private student entries out of school AI context. A “unified context” is role-filtered, not a single unrestricted prompt.

**Acceptance:** improve a goal using a real baseline, open the cited evidence, and show a clear request for missing data when no baseline exists. Test stale/conflicting sources, revoked access and embedded prompt instructions.

### C03 — Deliver reviewed state-form IEP and ETR packages

**Gate: D for demonstrated forms; P for the supported production package · Source: both**

The repository seeds a generic, state-less Default IEP. The PDF renderer uses flowing sections/fields and omits empty fields/sections. Claude reports that ETR and 504 creation failed in QA with “No document template is available.” That observation is environment-specific; it does not establish the contents of every deployment.

**Required outcome:** choose the launch state and package source/version/effective-date metadata, complete IEP and ETR content, guidance, validation and reviewed output. If Ohio is chosen, PR-07 and PR-06 are the starting packages; include supporting notices/consent documents needed by the pilot scope.

**Acceptance:** a school special-education reviewer checks populated exports against applicable forms, including long text, repeated goals/services, empty/non-applicable sections, participants, signature pages and page breaks. Older records retain their pinned form version. A generic fallback is labeled honestly.

District customization is a later architecture decision: current template uniqueness is state + document type, so district variants need an explicit schema/resolution model. Provide governed defaults/extensions without allowing local edits to silently replace required content. Add 504 only when included in the agreed scope.

### C04 — Connect parent viewing, deliberate draft sharing and AI review

**Gate: D · Source: both**

The API exposes child-linked authored versions, but the parent card/route uses legacy IEP versions. `AnalysisSourceType` only includes uploaded IEP, ETR and progress reports. There is no complete shared-draft lifecycle.

**Required outcome:**

- Parent route/card for template-authored documents, including the IEP/ETR types being sold.
- Deliberate **Share draft** action that previews recipients and publishes a fixed, coherent review revision. Later internal edits stay private until re-shared.
- Per-section/goal explanations and private AI questions over that exact shared revision, without downloading and re-uploading a PDF.
- Explicit parent responses/questions/change requests, with a staff review-and-response view. Private analysis is not automatically submitted.
- Revision comparison, acknowledgment tied to a revision, delivery status and revoke/expire behavior.
- Post-meeting family summary and final document access. Review, agreement, consent and signature remain separate events.

**Acceptance:** staff share a snapshot, parent explains a goal and submits one question, staff resolve it, and an unshared subsequent edit stays hidden. Repeat access checks for unrelated and revoked family accounts. Support school-only completion without a parent account.

### C05 — Complete student management and safe XLSX re-import

**Gate: D · Source: both; detailed repeat-upload safeguards from Codex**

Students can be created/viewed, but edit/archive/transfer and spreadsheet ingestion are missing. The school record lacks a stable external student identifier; the create UI omits some fields already present on the entity, including DOB.

**Required outcome:**

- Student edit, search, filters, pagination, archive/reactivate, exit and school transfer, preserving documents, links and history. Add controlled, jurisdiction-appropriate values for grade/disability rather than fragmenting analytics with unconstrained strings.
- A versioned XLSX template with instructions, examples and allowed values. Core fields: district student ID, school code, name, DOB, grade and status; scope optional language, case-manager and timeline columns deliberately.
- Match by **district + stable student ID**, preserving leading zeroes. Never match by name alone.
- Server validation, duplicate-ID detection, preview of new/updated/unchanged/error rows, downloadable correction report and explicit commit semantics.
- Identical re-upload creates zero duplicates. Missing rows do not imply deletion; blank cells do not silently erase data. Define explicit clearing, manual-edit conflicts and transfer/access behavior.
- Record import batch, actor, time, row outcomes and changes; retries must be safe. Validate size/schema and do not execute workbook formulas/macros.

**Acceptance:** upload a realistic roster, correct rejected rows, re-upload it unchanged, then change one student and transfer another. Show no duplicates and intact links/history. Duplicate merge needs an explicit, reviewable procedure.

Design the ingestion service for future SIS reuse. ClosedXML is Claude's candidate implementation library, not a selected dependency. Staff import and bulk case-manager assignment are useful next extensions; they do not replace safe student re-import.

### C06 — Model IEP teams and finish staff lifecycle

**Gate: D core team; P cross-school/membership lifecycle · Source: both; cross-school and existing-account constraints from Codex**

Organization roles are DistrictAdmin, SchoolAdmin and Teacher; per-student grants are Owner/Collaborator/Viewer. Staff profiles have one school association, cross-school assignments are restricted, and staff invites reject existing-account emails.

**Required outcome:**

- A student-centered team panel with one accountable lead case manager, providers, general educator, family contacts, student and interpreter/LEA roles as applicable.
- Search/add multiple members, reusable defaults and bulk assignment from the roster. Show responsibilities separately from permissions.
- Support a provider across buildings without granting district administration; distinguish organizational membership, school scope, student access and meeting participation.
- Safe role/school changes, reassignment and offboarding; preserve ownership of active work and a viable administrator.
- Defined existing-account and parent-plus-staff behavior, separate-household permissions, outside contributors and rights-holder changes.

**Acceptance:** create a team from the student page, sign in as each role and show the intended data/actions. A provider completes a scoped contribution without editing the whole IEP. Treat LEA participation as an assignable meeting responsibility where appropriate.

### C07 — Add meetings, deadlines, notifications and calendars

**Gate: D scheduling/core dates; P reliable delivery and rule governance · Source: both**

`MeetingPrepChecklist.MeetingDate` is a parent preparation field, not a scheduling service. Existing invite expiry/reminders do not provide student deadlines or meeting notifications.

**Required outcome:**

- Meeting with student, type, timezone/date/duration, location/video URL, status and related document revision; participants with team role, invitation/RSVP/attendance and external-contact support.
- Staff calendar and upcoming list; student meeting history; reschedule/cancel updates. Start with ICS invitations/export; a revocable per-user subscription feed is an option. Do not claim external calendar availability or two-way sync until implemented.
- Annual-review, reevaluation, evaluation, input and draft-review obligations with source event/date, owner, status and reviewed exceptions. Keep meeting dates distinct from procedural deadlines.
- In-app notifications and email/digests for actual actions: scheduled/changed meeting, shared draft, submitted response and upcoming/overdue work. Configurable reminders, deduplication, delivery status and retries.
- Jurisdiction rules are versioned and reviewed; do not hardcode example timeline/transition ages as universal requirements. Missing dates are unknown, not healthy.

**Acceptance:** schedule a meeting, show it on a participant's calendar, reschedule the same event, record attendance separately, and show an actionable deadline with its source. Verify actual invitation/update delivery in the target environment.

### C08 — Replace setup-only homes with actionable role views

**Gate: D, after C06/C07 data exists · Source: both; live polish observations from Claude**

Keep the current design system. Build the home around “what needs my attention,” with consistent scope/date filters, restrained status colors, a main work queue, and a secondary calendar/summary area.

| Persona | Home should answer |
|---|---|
| Steph — case manager | What meeting, draft, deadline or missing contribution needs action next? |
| Priya — provider | What specific goal/section input do I owe, and when? |
| Dennis — school admin/LEA | Which meetings need preparation, participants, decisions or signed records? |
| Karen — district director | Where are deadline exceptions, missing ownership, workload concentrations and rollout gaps? |
| Dana/Rosa — parent | What is coming up, which draft should I review, and what can I do now? |

Metrics need real records, defined denominators/date ranges and drilldowns. Examples: reviews due/overdue, known-date coverage, time to final record, baseline completeness, parent review and staff adoption by building. AI acceptance is a usage signal, not proof of quality. Avoid staff rankings or family-risk scores.

**Acceptance:** every alert opens its actual task/document/student; completing the task changes the dashboard. School-only families are not perpetual “no parent” problems. Loading failures must not become “No students yet” or misleading “not found” states.

Claude reports sidebar name clipping, repeated district headings and tab-branding gaps. Reproduce these observations at the target viewport before changing layout. Test crowded desktop staff views and phone parent reading, including keyboard, screen reader, zoom, save conflicts and recovery.

### C09 — Establish a shared profile and cross-year goal history

**Gate: D shared profile; P/E longitudinal depth according to sale scope · Source: both**

SchoolStudent, ChildProfile, parent advocacy goals, document goals and student voice entries represent different information and permissions. Linking them should preserve those distinctions.

**Required outcome:** shared strengths/interests, communication/access needs, family priorities, student voice, present levels and supporting evidence, each with source, author, date, review state and visibility. Define authoritative identity fields and a conflict workflow; do not silently overwrite parent-owned history when linking a school record.

Keep parent advocacy goals, student aspirations and measurable IEP goals distinct but linkable. Give IEP goals stable cross-year identities, baseline/unit/target, owner, observations, met/unmet rationale and carry/change/retire decisions.

**Acceptance:** an approved family/student contribution can inform a draft with attribution, a private note cannot, and a revised goal retains its history. A student changing schools does not lose the record or inherit unintended access.

### C10 — Complete evaluation, meeting and final-record procedures

**Gate: D demonstrate supported completion; P full promised lifecycle · Source: both**

A generic ETR editor is not an evaluation case, and a finalized PDF is not a complete meeting record.

**Required outcome:** referral/consent events and applicable clocks; evaluator assignment and submissions; approved assessment synthesis; human eligibility determination and rationale; ETR-to-IEP handoff; pre-meeting brief; attendance and structured decisions; reviewed document changes; notices; signed artifacts; amendments with reason/effective date and relationship to the prior record.

Separate invalid data and unsafe operations from advisory professional-completeness warnings. Current required-field/schema validation can block finalization; define which warnings allow a recorded professional override rather than promising unconditional bypass.

**Acceptance:** school and family can retrieve the exact reviewed/signed record and relevant evidence. Print/sign support includes version-associated signed upload and outstanding-signature status. School-only operation supports offline input/contact attempts and complete export without requiring portal participation. A district collaboration policy needs lifecycle/access behavior, not just a boolean.

### C11 — Provider progress, annual renewal, language and adoption

**Gate: P where promised/needed; E for broader coverage · Source: both**

- **Progress/annual review:** reuse existing report/comparison features; add quick goal observations, provider tasks, trajectories, reporting-period summaries, service-delivery records where in scope and reviewed carry-forward. Missing observations produce “insufficient data,” not invented progress.
- **Language:** agree pilot languages, preserve originals, translate authorized invitations/draft explanations/questions with reviewed terminology. Translation on save must not expose an unshared working draft. Live translation/transcription can wait; participating families' actual needs cannot.
- **Student voice:** integrate existing privacy/share controls; extend preparation and accommodation feedback as validated by users.
- **Adoption:** short role-specific onboarding and direct provider task links. Evaluate SSO/magic links with the buyer early; mandatory SSO is a pilot gate, not automatically an expansion item.

**Acceptance:** a provider records one goal observation quickly, a parent can understand the resulting trend, and a case manager can reuse evidence in the next review. Validate with the director, case manager and parent advocate identified in the personas, plus providers/multilingual families where in scope.

## 6. Real-data pilot and commercial-readiness gates

These findings concern evidence and operational completeness. Neither audit established a breach or certified compliance.

| Gate | Finding and required outcome | Source |
|---|---|---|
| Access and support boundaries | Existing server-side scoping/MFA/revocation are strengths. Test two districts, schools, assigned/unassigned staff, households, student sharing, AI context and direct downloads/API calls. Define privileged support access, reasons, duration and audit. Claude raises broad internal/QA access; verify actual entitlements rather than assuming all admins can read everything. | Both |
| Durable audit evidence | In-memory fire-and-forget audit queue and dropped failed inserts can lose events. Persist critical evidence durably, retry/reconcile and test restart/database failure. | Codex |
| Record integrity | ORM immutability can be bypassed by raw SQL/bulk operations. Enforce appropriate database/storage privileges and integrity/recovery controls; verify correction procedures. | Both |
| Delivery reliability | Missing email configuration can silently no-op; provider exceptions are caught. Require visible failures/retry, configuration checks and real staff/parent/meeting-message delivery tests. | Codex |
| District exit/export | Existing parent export is incomplete for a school record system. Provide usable district/student packages with documents, sources, versions, signed artifacts, relevant evidence and manifest. | Both |
| Account closure/recovery | Account deactivation/token revocation conflicts with the authenticated deletion-cancellation path; no purge worker was found. Implement and test the chosen closure/cancellation lifecycle separately from school retention/holds. | Codex |
| Privacy/telemetry and AI governance | Sentry default PII and user ID/email reporting need an inventory/redaction review; student-content leakage was not demonstrated. Document processors, retention, model data use and approvals accurately. Do not publish a “no training” claim without verifying actual terms/settings. | Both; telemetry detail Codex |
| Release checks | Deployment workflows lack explicit test gates. Add required backend/frontend tests, critical cross-role journeys, production-database migration validation, migration/rollback procedures and smoke checks. GitHub protections were not inspected. | Both |
| Recovery/support | Rehearse restore/export and incident handling; name support ownership, queue/processing alerts and recovery expectations. A runbook alone is not proof. | Both; detail Codex |
| Procurement and billing | Define supported state/forms/users, pilot terms, migration/onboarding, support, exit and district entitlements. Manual invoicing can suffice. School-sponsored parents should not hit an unexpected individual subscription paywall; budget organization AI usage. | Both |
| Trust/accessibility materials | Prepare reviewed data-handling/security/AI documentation, an accurate trust page, accessibility information and contractual/data-processing materials. Claims should match evidence. | Both |
| School-facing marketing | Add a school track to the parent-oriented marketing site: buyer value, supported scope, demo/pilot path and consistent school messaging. | Claude |

### Release and demo hygiene to retain from both audits

- **Fictional demo environment with reset/seed tooling:** realistic schools/staff/students, prior IEP/ETR evidence, deadlines, meetings, shared revisions and separate role accounts. Claude reports seeing a real child's name in QA; that identity was not reverified here. Use synthetic data for sales regardless.
- **Environment alignment:** Codex's esbuild mismatch must be resolved in its environment. Claude reports local port 7000 interference; `web/vite.config.ts` targets 7000 while the runbook/E2E expect 7200. Align supported startup configuration and document it.
- **Reproducible content:** Claude found no code-seeded knowledge-base content. Check seed/provisioning ownership for templates, help content and demo fixtures so a fresh environment works.
- **Existing review debt:** triage and resolve the five `todos/P2-*` files covering shutdown classification, invalid-response refunds/unmetered spend, refund ordering, SDK exception classification and duplicated classification. Recheck whether findings remain open before treating them as fresh defects.
- **Documentation/support:** update stale root README/PLAN/PROGRESS material and supported startup instructions; the README references a mobile app. Replace or operationalize the Google Form support link with clear ownership and response expectations. Preserve useful history rather than deleting documents indiscriminately.

## 7. Persona and journey corrections

Both source audits agree that the personas identify the right jobs, but their August “Gap vs. today” tables need updating. They are explicitly unvalidated design inputs, not proof of buyer preferences.

| Journey/persona area | Corrected assessment |
|---|---|
| J1 parent-only adoption | Existing upload/analysis/prep is a useful foundation. Preserve history/privacy when school linking arrives; verify mobile, language and recovery. |
| J2 school onboarding | Three org roles and differentiated admin/teacher homes already exist. XLSX replaces the earlier CSV requirement; bulk import/team lifecycle/first educator win are still missing. |
| J3 evaluation | Generic ETR infrastructure exists, but ready templates and evaluation-case workflow are incomplete. |
| J4 collaborative IEP | AI/student voice are not globally absent; they are disconnected from the default editor. Parent sharing/review and authored-revision analysis remain missing. |
| J5 school-only | PDF export now exists. State-form verification, supported participation policy and complete record procedures remain gaps. |
| J6 meeting day | Parent prep has a date; operational meetings/calendar/attendance do not. |
| J7 progress | Report capabilities exist; provider capture, goal trajectories and complete longitudinal workflow need work. |
| J8 annual review | Comparison/history exist; persistent goal identity, review dates and evidence-driven renewal are incomplete. |
| Sam/platform support | Distinguish visible audit logs from durable/tamper-resistant evidence, and support metadata from privileged content access. |

Preserve the accepted decisions: system of record; whole-draft sharing by deliberate school action; human approval of AI; private family preparation; student sharing by choice; first-class school-only and parent-only operation.

## 8. Delivery sequence and decisions for review

### Decisions to settle before implementation

1. **Launch scope:** Ohio-first recommendation, supported IEP/ETR forms and student populations; whether 504 is included.
2. **Authoring architecture:** structured blocks versus domain entities with template mappings; migration/history strategy.
3. **Identity and access:** authoritative profile fields, district student key, cross-school staff membership, functional team roles, separate households and school-sponsored parent access.
4. **Record lifecycle:** shared-draft revisions, professional-warning overrides, signature/offline procedures, amendments, collaboration policy and district export.
5. **Pilot requirements:** languages/accessibility, mandatory SSO, progress/service-delivery scope, support/procurement and success measures.

### Phase A — Repair the core and make environments reproducible

C01/C02 authoring integration and prefill; C03 launch templates; C04 parent finalized-document access; synthetic seed/reset; dependency/port repair; required CI checks. Agree shared models before independent tracks diverge.

**Exit:** staff create a populated template document, use grounded assistance and export it; a linked parent can open the authored version through normal UI. This is a capability milestone, not the full requested demo.

### Phase B — Complete the integrated school story

C05/C06 roster and teams; C07 meetings/deadlines/notifications; C08 dashboards on that data; C04 whole-draft parent review; C09 shared context; reviewed C03 output and the supported C10 completion path.

**Exit:** every step of the demo below works in the default UI, including repeat import, parent AI/questions and school-only completion. Test failure/retry and negative access paths.

### Phase C — Qualify a bounded paid pilot

Complete the record procedures and staff lifecycle promised to the buyer; close section 6 gates; exercise real delivery, restore/export and migration; validate languages/accessibility/SSO and annual-cycle scope. Run acceptance with actual school/family representatives and record results/owners.

**Exit:** written commercial scope, accepted workflows, real-data operational proof and agreed onboarding/exit procedures. Completion of features alone does not guarantee procurement approval.

### Phase D — Expand from measured pilot evidence

SIS sync, two-way calendars, more states/languages, governed district variants, 504, deeper provider/service tracking and annual renewal. Move any buyer prerequisite into Phase C. Defer meeting recording/live interpretation and speculative predictive analytics until need, quality and policy are established.

## 9. Integrated demo and acceptance measures

Use a fictional district with realistic existing data, at least two staff roles, a provider, an administrator, an online family and an offline family. Aim for a rehearsed **15–20 minute** core story; this is a proposed target, not measured current performance.

1. **Administrator dashboard:** show sourced upcoming work and an ownership exception; drill into the actual student.
2. **XLSX import:** preview/commit a roster, re-upload unchanged, then update one record without duplication or lost history.
3. **Team:** assign case manager/provider/general educator/LEA/family contacts; demonstrate scoped access.
4. **Schedule:** choose participants/time/location; show calendar and actual invitation/reschedule artifact.
5. **Student evidence:** show strengths, family/student contributions and prior IEP/ETR evidence with privacy/source labels.
6. **Author:** open a populated state IEP through ordinary navigation; improve a measurable goal with cited AI, and show a missing-baseline case. Demonstrate the supported ETR drafting slice too.
7. **Share/review:** share one revision; parent on a phone explains a goal, asks private questions and submits one selected response. Internal edits remain hidden until re-shared.
8. **Resolve/complete:** case manager addresses the question; LEA reviews the brief; record the outcome and export the reviewed state form with the supported signed-record procedure.
9. **School-only:** complete/export for the offline family without requiring a portal account.
10. **Close the loop:** return to the dashboard and show the task/status changes caused by the demonstration.

### Pilot measures

- Time from roster receipt to first completed educator task; import correction rate and zero duplicate students on identical re-upload.
- Authoring time versus the school's baseline; factual-correction rate and usable source references for AI suggestions.
- Named case-manager coverage, known deadline coverage, actual overdue work and time from meeting to reviewed final record.
- Family review/question completion with offline participation recorded separately; provider contribution effort and adoption by building/role.
- Correct cross-role access denials, actual notification delivery, tested restoration and usable school exit packages.

**Demo-ready:** this complete story works with synthetic data through normal UI and reviewed output.  
**Paid-pilot-ready:** demo-ready plus supported record lifecycle, operational/access evidence, school acceptance and written scope.  
**Broader replacement-ready:** successful pilot evidence plus the annual-cycle workflows, jurisdiction coverage and integrations promised to subsequent buyers.

## 10. Traceability

| Combined item | Claude source sections | Codex source sections |
|---|---|---|
| C01–C02 authoring/AI | F1; Goals 5/7; Phase 0 | G01/G08; empty-draft finding |
| C03 state packages | Goal 5; Phase 5; template appendix | G02/G09 |
| C04 parent review | F1; Goal 6; Phase 4 | G03 |
| C05 roster | F3; Goal 2 | G04 |
| C06 team/access | F3; Goal 1; open decisions | G05 |
| C07 time/calendar | F2; Goal 4; additions 1/4 | G06 |
| C08 dashboards | Goal 3; walkthrough appendix | G07 |
| C09 shared profile/goals | F1; Goal 7 | G08 |
| C10 record lifecycle | Goal 5; additions 3; Phase 5 | G09 |
| C11 annual cycle/language | Personas review; additions 6/8 | G10/G11 |
| Pilot/commercial/hygiene | Additions; appendix; inventory | Sections 2/6/7 |

For detailed file/line evidence, retain the linked original reports. High-value implementation entry points include `web/src/config/features.ts`, `web/src/app/routes.tsx`, the `document-authoring` and `iep-authoring` features, and the backend `DefaultIepTemplateSeeder`, `DocumentInstanceService`, `IepAssistService`, `AuthoredDocumentVersionService`, `AnalysisRunService`, `EducatorService`, `OrgAccessService`, `EmailService`, `AuditLogger` and `AccountService` implementations.
