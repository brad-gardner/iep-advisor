# School demo and sales readiness: gap analysis

**Audit date:** September 15, 2026  
**Repository baseline:** `590badd`  
**Scope:** Current implementation, all eight personas and eight journeys, school onboarding/runbooks, and Brad's seven school goals. This is a product and implementation audit, not a certification of legal compliance or a production penetration test.

## 1. Executive assessment

**There is a substantial foundation, but the school experience is not yet a complete, demonstrable IEP/ETR workflow.** The largest gap is connecting existing capabilities into the everyday work of a case manager, parent, and administrator. A cosmetic dashboard redesign alone will not get the product ready to sell.

The repository already includes district setup, school management, staff invitations, scoped student access, parent/student linking, uploaded-document analysis, authoring, versioning, PDF generation, and a reusable UI system. Preserve and extend these investments.

The most consequential discovery is that **the default template authoring experience does not expose the educator AI and student-input capabilities built into the legacy IEP editor.** An inventory of features would suggest more readiness than a customer following the normal navigation would encounter.

The personas and journeys explicitly commit to being **the system of record**, including school-only operation. That makes usable state-form output, document lifecycle, record integrity, migration, and district exit/export essential to the sales proposition. A narrowly scoped paid pilot is a sensible first commercial milestone, but its supported state, forms, users, and operating procedures must be explicit.

### Recommended order

1. Connect template authoring, student context, AI assistance, and deliberate parent draft review.
2. Complete roster management with repeatable XLSX import and a student-centered team workflow.
3. Add meeting scheduling and deadline/task records; build the professional dashboards on those real records.
4. Prove one state's IEP and ETR output, including school-only completion and signed-record handling.
5. Close the operational, access, evidence, and procurement gaps required for real student data.
6. Extend into provider progress capture and annual-review continuity; defer broader integrations until the core loop works.

**Readiness judgment:** suitable for demonstrating selected capabilities today after environment repair; not yet supported by this audit as a complete school replacement product. Target an integrated demonstration, then a bounded paid pilot, then broader replacement sales. These are separate acceptance gates, not interchangeable labels.

## 2. Evidence and verification

Findings distinguish **implemented** (code exists), **partial** (some workflow exists), **missing** (not found in reviewed routes, services, models, and feature searches), and **unverified** (requires environment, user, or external review). “Implemented” does not mean production-proven.

| Check | Result |
|---|---|
| Backend service tests | `dotnet test api/IepAssistant.Services.Tests/IepAssistant.Services.Tests.csproj --verbosity minimal`: **485 passed, 0 failed, 0 skipped**. One nullable-reference warning in `IepProcessingService.cs:57`. |
| Frontend TypeScript | `npx tsc -b` in `web`: **passed**. |
| UI consistency guard | `npm run guard:ux` in `web`: **passed**. This checks selected design-system conventions, not visual quality or accessibility conformance. |
| Frontend production build | `npm run build`: **blocked** at Vite startup by local esbuild mismatch: host `0.28.1`, binary `0.25.11`. TypeScript completed before that failure. |
| Frontend unit tests | `npm test`: **blocked** by the same esbuild startup mismatch; no passing frontend-test claim. |
| Browser/E2E, production configuration, real email, visual assessment | **Not executed/verified in this audit.** Existing test files and runbooks were inspected. Dashboard recommendations reflect implementation and persona needs, not a rendered screenshot review. |

The esbuild failure is an observed local installation problem, not proof of a source-code build defect. Repair/reinstall dependencies from the lockfile and repeat build/tests before the demo. The July [pilot runbook](../ops/2026-07-01-pilot-golden-path-runbook.md) still records its live walkthrough as pending; unchecked runbooks are not launch evidence.

Source paths below are relative to the repository root; line references identify the inspected baseline. No application changes were made for this audit.

## 3. Coverage of the requested school goals

| Goal | Current state | Gap to a credible demo/sale |
|---|---|---|
| Users, roles, intuitive IEP team | **Partial.** Organization roles, invitations and per-student access exist. | Functional team roles, lead case manager, easy multi-person assignment, staff role/school changes, cross-school providers, and clear family permissions. |
| Students and re-uploadable XLSX roster | **Partial student CRUD; import missing.** Creation/viewing exist. | Edit/archive/transfer; stable district student ID; template download, validation, preview, repeatable updates, import history. |
| Professional dashboard, alerts, analytics | **Partial.** Admin setup/roster tiles and reusable UI primitives exist. | Case-manager work queue, meetings, actual deadlines, document/review status, meaningful drilldowns and honest denominators. |
| IEP scheduling and staff calendar | **Missing operational workflow.** Parent preparation has a meeting-date field. | Meeting entity, participants, roles, invitation lifecycle, staff calendar, reschedule/cancel, reminders, attendance. |
| State-form IEP/ETR drafting with contextual AI | **Disconnected partial capabilities.** Template engine/PDFs and legacy IEP AI exist. | AI in default editor, validated state content/output, ETR workflow, reusable student evidence, source-linked suggestions. |
| Send draft to parents for review and AI understanding | **Partial final-version access; requested draft loop missing.** | Deliberately shared draft snapshot, parent route, private analysis/questions, explicit question submission, revisions and acknowledgment. |
| Shared student goals/bio used by AI | **Partial and fragmented.** School profile, parent child profile, document goals and student voice entries exist. | Agreed shared profile, field provenance/visibility, persistent goal identity, role-filtered AI context and approval of suggested changes. |

## 4. Personas and journeys: what to keep and correct

The [persona index](../personas/00-persona-index.md) correctly distinguishes the buyer from the daily author. The [journey index](../journeys/00-journey-index.md) correctly treats the annual cycle as the product's organizing structure. Both explicitly say their assumptions are unvalidated. Keep that distinction when choosing scope and making sales claims.

### Persona implications

| Persona | What must be convincing |
|---|---|
| Karen — district director/buyer | Upcoming/overdue work, evidence retrieval, controlled access, rollout progress and workload distribution. Student/staff counts alone do not establish the promised value. |
| Steph — case manager | A populated draft, source-aware goal assistance, no lost edits, team input without chasing, and fast completion with or without a parent account. Measure actual authoring time saved. |
| Dennis — school admin/LEA | Read-mostly meeting brief, participants and unresolved issues, proposed commitments, and an explicit reviewed outcome/signature step. |
| Priya — service provider | Cross-building access and direct entry to one assigned contribution. A provider should not need full document-editing responsibility to submit progress. |
| Dana — parent | Clear shared-versus-private boundaries, plain-language explanation, questions attached to the right draft, and continuity of parent-owned history when a school joins. |
| Rosa — multilingual parent | Preferred language, translated invitations and draft explanations with originals available, bilingual questions and human review of important communications. |
| Alex — transition-age student | Preserve the existing private/shared voice workflow, connect approved input to authoring, and distinguish sharing with a team from publishing in a document. |
| Sam — platform support | Processing/delivery health, access explanations, logged support actions, recovery tooling, and complete export/offboarding operations. |

### Journey coverage after checking code

| Journey | Assessment |
|---|---|
| J1 Parent-only adoption | Upload/analysis/preparation foundation exists. Still needs reliability/mobile/language validation and preservation when linking to school records. Maintain Mode C as promised. |
| J2 School onboarding | Organization setup exists; bulk import, staff lifecycle/team roles and a meaningful first educator task are the principal gaps. |
| J3 ETR/eligibility | Uploaded ETR analysis and generic document infrastructure exist. Evaluation case, evaluator contributions, consent-triggered workflow, determination and ETR-to-IEP handoff remain gaps. |
| J4 Collaborative IEP | Strongest opportunity, but default editor, AI, student voice and family review are disconnected. Whole-draft review is missing. |
| J5 School-only | PDF export now exists; state-form completeness and procedural/signed-record handling need proof. Parent non-participation must be a supported state. |
| J6 Meeting day | Parent prep exists. Operational scheduling, attendance, school meeting workspace and outcome reconciliation are missing. |
| J7 Progress monitoring | Report upload/viewing/analysis and report-entry capabilities provide a seed; longitudinal goal data, provider session capture and service-delivery workflow remain incomplete. |
| J8 Annual review | Document comparison/history exist. Persistent goal trajectories, review clocks, carry-forward decisions and source-based next-draft creation remain gaps. |

### Documentation drift and unresolved decisions

- **“No educator AI” is stale.** AI exists in `IepAssistService` and the legacy editor. The remaining issue is integration into the active template path and richer evidence context.
- **“No export” is stale.** Both legacy and generic authored-version PDF services exist. Generic PDF output is not proof of state-form fidelity.
- **“Student participation missing” is stale.** `StudentWorkspaceEntry`, `StudentWorkspaceService`, student pages and legacy pull-from-student components exist. Integrate them instead of rebuilding student voice.
- **“One educator role/home” is too broad.** DistrictAdmin, SchoolAdmin and Teacher organization roles and different admin/teacher homes exist. Functional case-manager/provider/LEA roles and useful daily work surfaces remain missing.
- **“No meeting date anywhere” is too broad.** `MeetingPrepChecklist.MeetingDate` exists; a scheduled meeting with participants and calendar behavior does not.
- **J2's CSV assumption is superseded by this request:** deliver a downloadable, re-uploadable **XLSX** template. CSV can be an additional format later.
- **Validation policy conflicts:** Steph's persona says advisory warnings must not block professional judgment; `AuthoredDocumentVersionService` currently blocks finalization on required-field/schema errors. Separate invalid data/unsafe operations from professional-completeness warnings and define a reasoned override where appropriate.
- **Translation timing needs clarification:** Rosa's persona describes translation on save, while the standing decision requires deliberate whole-draft sharing. Translate only content the reader is authorized to see; saving an internal draft must not publish it.
- **Parent review is not consent or signature.** Model draft sharing, review acknowledgment, consent, meeting attendance and execution as distinct events.

Update the original gap tables after implementing these decisions; this audit does not silently rewrite the accepted product vision.

## 5. Prioritized implementation gaps

**D = required for the integrated school demo. P = required before the supported real-data paid pilot. E = expansion after the pilot unless a buyer makes it a prerequisite.** These labels are product gates, not security severity scores. Effort is relative: M = bounded workflow; L = coordinated model/API/UI work; XL = multiple connected workflows. They are not delivery estimates.

### G01 — Bring AI and student context into the default authoring workflow

**Gate: D · Effort: L · Personas: Steph, Alex · Journeys: J3/J4/J5**

Evidence: `web/src/config/features.ts:13` selects `template`; `educator-student-detail-page.tsx:234` hides the typed editor link in that mode. `document-authoring/components/document-editor.tsx` renders fields, autosave and finalization without AI or student input controls. The legacy `iep-authoring-workspace-page.tsx` exposes chat; its field editors expose assist and pull-from-student controls.

`IepAssistService.cs:205` constructs goal prompts from the current goal fields; chat at `:301` uses the current typed draft. That is useful editing assistance, but not the promised synthesis of profile, prior IEP, ETR and progress evidence.

New template documents also start with `ValuesJson = "{}"` (`DocumentInstanceService.cs:84`). The first-draft experience is still empty. Populate verified demographics and propose relevant prior content/evidence with clear source and staleness labels; do not silently copy last year's goals forward.

**Acceptance:** from ordinary student navigation, create an IEP/ETR, see relevant student evidence next to a field, request an explanation/rewrite/proposal, inspect source references, and accept/edit/reject it. Explicitly report missing baseline evidence. Do not fabricate measurements. Store source/version references and approval provenance; filter context by the caller's access before sending it to AI. Preserve the existing human-accept pattern.

Use small React components for evidence panels, assist controls, proposal comparison and status; avoid turning the generic field renderer into a second monolithic editor.

### G02 — Deliver one verified state document package

**Gate: D for demonstrated forms; P for supported production scope · Effort: XL · Journeys: J3/J4/J5/J6**

Evidence: `DefaultIepTemplateSeeder.cs` seeds a **state-less Default IEP**, with all fields optional. The template resolver supports state selection/fallback, but reviewed code does not prove a packaged, published state IEP/ETR library. `AuthoredDocumentPdfDocument.cs:85` uses a generic flowing Letter layout; empty fields/sections are omitted. A state dropdown, document-type row or generic PDF is not evidence of a complete prescribed form.

**Acceptance:** choose a launch state; package versioned IEP and ETR templates, required supporting forms in the pilot scope, field guidance, form identity/effective dates and approved rendering. A special-education reviewer checks representative exports against the source forms, including long narratives, repeating goals/services, blank/non-applicable sections, participants, signatures and page breaks. Pin old records to their original template versions.

**Ohio is an illustrative launch-state option, not an assumed business decision.** The official forms page lists PR-06 ETR, PR-07 IEP and supporting forms. It also announces revised statewide forms, optional for 2026–27 and required beginning 2027–28. This makes form-version management a near-term product requirement if Ohio is selected. [Ohio Department of Education and Workforce — Required and Optional Forms](https://education.ohio.gov/Topics/Special-Education/Federal-and-State-Requirements/Ohio-Required-and-Optional-Forms-Updated) (checked September 15, 2026).

Do not advertise broad state coverage based on the generic template engine. Verify the chosen jurisdiction and pilot procedures with the school before setting contractual scope.

### G03 — Add deliberate whole-draft parent review

**Gate: D · Effort: L · Personas: Dana, Rosa, Steph · Journey: J4**

Evidence: `AuthoredDocumentVersionService.cs:254` grants child-linked access to finalized versions. The registered parent version page is the legacy IEP version route; generic authored-version UI is registered under the educator role in `web/src/app/routes.tsx`. The current lifecycle does not provide the requested coherent shared-draft review loop. Finalized-version access is link-based, without a separate whole-draft release step.

Parent AI has a separate integration gap: `AnalysisSourceType.cs` only includes uploaded IEP, ETR and progress-report sources, and `AnalysisRunService.cs:331` handles those types. Add authorized shared authored revisions as analysis sources so families do not have to download and re-upload the school's PDF to understand it.

**Acceptance:** case manager selects **Share draft**, previews recipients and the exact immutable review snapshot, and sends a secure invitation. Parent opens that snapshot on a phone, gets plain-language section/goal explanations and private AI assistance, and deliberately submits selected questions. Staff see submitted questions and response status; private preparation remains private. New internal edits remain hidden until another explicit share. Show what changed between shared revisions and which revision each acknowledgment concerns. Define revoke/expire behavior and record delivery failures.

Enable parent access to the generic document types being sold, including ETR where in scope. School-only completion must work without creating a parent account. Distinguish “reviewed” from “agreed” and “signed.”

### G04 — Make roster import safe to repeat, and complete student management

**Gate: D · Effort: L · Personas: Karen, Dennis · Journey: J2**

Evidence: `SchoolStudent.cs` has no stable external roster identifier. `EducatorController.cs:39–82` exposes list/create/detail; student update/archive/transfer and XLSX import workflows were not found. `EducatorService.cs:137–155` loads the scoped roster without server pagination.

**XLSX contract:**

- Download a versioned workbook with Instructions, Students and allowed-value examples. Suggested student columns: district student ID, school code, first/last name, date of birth, grade and active status. Keep sensitive optional data minimal; represent identifiers as text to preserve leading zeroes.
- Match by **district + stable student ID**, never by name alone. Import updates must target the same student after a school transfer. Establish this contract now for future SIS integration.
- Validate workbook schema, duplicate IDs, school scope, dates and values; reject executable/macro content and enforce size/row limits. Do not evaluate spreadsheet formulas as student data.
- Preview creates, updates, unchanged rows and errors before commit. Provide row/column errors in a downloadable correction report. Choose and document atomic versus explicitly selected valid-row commits.
- Re-uploading the same file creates zero duplicates. Changed rows update the same records. Missing rows do not delete/archive anyone; blanks do not silently erase existing values. Provide an explicit clearing rule.
- Preserve manual edits through conflict detection/field ownership rules; record who imported what, when, and the outcome. Make failed/retried imports safe and corrections traceable.

**Student-management acceptance:** edit, search, filter, paginate, archive/reactivate and transfer a student while retaining documents, links and history. Preview the effect of a school transfer on staff access. Defer SIS transport, not identity and conflict semantics.

### G05 — Separate the IEP team from permission grants

**Gate: D core team; P staff lifecycle/cross-school support · Effort: L · Journeys: J2/J3/J4/J6**

Evidence: `OrgRole.cs` defines three organization roles. The assigned-staff form uses Owner/Collaborator/Viewer. `StaffProfile.cs` has one school association; `EducatorService.cs:242–251` rejects cross-school staff assignment. `StaffInviteService.cs:117,508` rejects existing-account emails. These are real constraints, not just labeling issues.

**Acceptance:** one student team panel assigns a lead case manager, providers, general educator, LEA representative, family contacts and interpreter as applicable. Functional role and data permission are shown separately. Support reusable team defaults and quick multi-person assignment. A provider can serve two schools without district-admin privileges. Staff role changes, reassignment and deactivation preserve an accountable owner for active work.

Define existing-account onboarding and parent-plus-staff membership; invitations should not force people to invent another email identity. Define access for separate households, outside participants and rights-holder changes with the pilot school. An LEA representative may be a role on a particular meeting, not a global administrator permission.

### G06 — Add meetings, calendar and actionable timeline records

**Gate: D scheduling/core deadlines; P reliable reminders and rule governance · Effort: XL · Journeys: J3–J8**

Evidence: `MeetingPrepChecklist.cs:8` has a parent preparation date. No operational meeting/participant/calendar domain was found. Existing staff-invite reminders are not meeting reminders or a student timeline engine.

**Acceptance:** schedule an IEP or ETR meeting from a student; select participants/roles; capture timezone, duration, location/video URL and interpreter need. Display each staff member's week/month calendar and the student's meetings. Send invitations and calendar artifacts; record RSVP separately from attendance. Rescheduling/cancellation updates the existing invitation rather than creating disconnected events. Support reminders with delivery status, retries and preferences.

Start with ICS invitations/export and detection of conflicts among meetings stored here. Defer two-way Microsoft/Google integration; do not claim external-calendar availability until connected.

Store annual-review, reevaluation, input and draft-review due dates with owner, originating event, source and status. When dates derive from jurisdiction rules, version those rules and record reviewed exceptions; do not treat a missing date as “on track.” A meeting date and a procedural due date are different records.

### G07 — Build professional homes around work to do

**Gate: D · Effort: M after G05/G06 · Personas: Karen, Steph, Dennis, Priya**

Evidence: `educator-dashboard.tsx:38–65` gives teachers one caseload-link card. Admin tiles show schools, staff, invitations and students without staff/parent links. The reusable `web/src/components/ui` layer and passing UX guard are useful foundations.

**Proposed dashboard hierarchy:**

1. Header with school/district scope, useful date range and one primary action.
2. Compact summary: overdue items, upcoming reviews, drafts awaiting action and scheduled meetings. Unknown data must be labeled.
3. Main work queue: student, action needed, reason, due date, owner, status, next action.
4. Secondary calendar and neutral workload/engagement summaries, with drilldowns.

Case managers see their next actions; providers see contributions owed; school admins see meeting readiness; district directors see exceptions and workload by building. Every alert opens the exact task/student/document. Define metric denominator and date range. Do not rank staff productivity or score family “risk.”

Use consistent spacing, typography, status language, restrained colors, readable tables, accessible focus and purposeful empty/loading/error states. Design and visually review desktop staff and phone parent flows with realistic, crowded data—not only empty accounts.

Fix deceptive error states first: `educator-students-page.tsx:59–62` can turn a network failure into “No students yet”; related detail/access loading paths also hide failures. A failed fetch should offer retry, not encourage duplicate data entry.

### G08 — Establish shared student context and durable goals

**Gate: D shared bio/context; P goal continuity for progress scope · Effort: L · Journeys: J3/J4/J7/J8**

Evidence: school and parent profiles, parsed goals, authored goals and student workspace entries are separate models. `IepAssistService` uses draft text/fields, not a unified student evidence record. Existing student entries already support private/shared boundaries.

**Acceptance:** student overview includes strengths, interests, communication/access needs, family priorities, student voice, current present levels and relevant evidence. Each contribution has author/source, date, review status and visibility. Link school/parent records without silently overwriting either party's history or exposing private notes.

Provide a role-filtered AI context panel showing exactly what evidence is available. Resolve conflicting or stale data explicitly. Keep **parent advocacy goals**, **student aspirations**, and **measurable IEP goals** distinct while allowing deliberate links.

Give measurable goals a stable identity across document revisions and years, including baseline/unit/target, owner, progress observations, status and carry/change/retire decisions. This enables meaningful progress and annual-review analytics rather than counting documents.

### G09 — Complete the meeting-to-record lifecycle

**Gate: P; demonstrate the supported completion path at D · Effort: XL · Journeys: J3/J5/J6**

Version snapshots and PDF rendering are useful foundations. They do not establish a complete process for evaluation consent, eligibility rationale, meeting decisions, signatures, prior written notice or amendments.

**Acceptance:** support a reviewed meeting brief; participant/attendance record; structured decisions and unresolved items; proposed document changes accepted by the author; and an immutable final record with associated notices and signed artifacts. If the pilot uses print/sign, provide a complete packet, signed-document upload, signatory/date/version association and an outstanding-signature queue. Add e-signature when required by the launch scope/buyer. Never treat clicking Finalize as a signature.

For ETR, add evaluator assignment/submission, source assessments, determination/rationale and an explicit handoff of approved findings into IEP drafting. Re-finalization alone is not an amendment workflow: record the reason, scope, effective date and relationship to the prior record.

Represent school-only/non-digital participation as a supported choice. Current no-parent attention lists should not continually flag families who intentionally use offline review. Log contact attempts and offline input without blocking school completion on portal activity.

### G10 — Finish provider progress capture and annual renewal

**Gate: P if selling full annual-cycle replacement; otherwise E with explicit pilot boundary · Effort: XL · Journeys: J7/J8**

Use the existing report and comparison features. Add quick goal-linked observations/session notes, attributed provider contributions, reporting-period summaries, trajectory charts, reminders and reviewed carry-forward into the next draft. Track service delivery separately from the text prescribing services when that is in the supported scope.

**Acceptance:** a provider follows a task link and records a data point without editing the whole IEP; a parent sees an understandable goal trend; the case manager produces a report from recorded evidence; the next annual review preserves rationale and flags insufficient data. Do not generate progress judgments from missing observations.

### G11 — Language, accessibility and adoption

**Gate: P for participating families' needs; broader coverage E · Effort: L**

The personas promise bilingual participation, while no end-to-end localization workflow was found. Agree the first pilot's languages and accessibility needs. Build language preference, translated invitations, authorized draft explanations/questions, original-language preservation and reviewed terminology. Human interpretation remains a distinct workflow; live translation/transcription is not necessary for the first integrated demo.

Verify keyboard completion, focus management, screen-reader labels, contrast, zoom, long documents, save/conflict recovery, phone reading and shared-device logout/privacy. Provide short role-specific onboarding and help inside the task. The existing component guard cannot establish these outcomes.

## 6. Real-data pilot and sales gates beyond visible features

These are evidence-backed operational gaps or diligence items, not claims that a breach occurred. Resolve them before relying on the product as the supported record system.

| Area | Evidence / concern | Required proof or action |
|---|---|---|
| Access boundaries | `OrgAccessService` uses active database profiles and district/school/student scope; auth includes MFA, lockouts and security-stamp revocation. Good foundations. New routes will expand the surface. | Exercise a permission matrix across two districts, schools, assigned/unassigned staff, households and students. Test direct API access, guessed IDs, revocation, deactivation, AI context and downloads—not only hidden navigation. |
| Durable audit evidence | `AuditLogger.cs:18–32` queues in memory; `AccessAuditLogWorker.cs:50–57` drops failed writes. | Persist critical events durably with retry/reconciliation; test restart/database failure. Distinguish access logging from approval/share/signature evidence. Provide district/student evidence export. |
| Record immutability | `ImmutableVersionInterceptor.cs` protects ORM writes but explicitly notes raw SQL/bulk-operation bypass. | Document/enforce database and storage privileges, retention and integrity controls appropriate to the promised immutable record; verify recovery and approved correction paths. This is not proof that ordinary users can mutate finalized records. |
| Email delivery | `EmailService.cs:359–390` can no-op without configuration and catches delivery failures; runbook explicitly requires real ACS verification. | Visible queued/sent/failed states, retry/support handling, correct environment URLs and a real staff/parent invite plus meeting update delivery test. A success response must not be mistaken for delivery. |
| Export and exit | `AccountService.cs:29–163` exports a parent-owned subset, not a complete district record package. | District/student export including authored documents, source files, versions, notices, signatures, relationships, relevant audit/AI provenance and manifest. Prove a departing school can recover usable records. |
| Account deletion/recovery | `AccountService.cs:186–192` deactivates/revokes the account while offering a cancellation window; cancellation requires authenticated access, while inactive login/token access is rejected. No deletion execution worker found by `DeletionRequestedAt` references. | Fix and test the actual cancellation/recovery path; define approved account deletion versus district record retention; implement and verify the chosen lifecycle. Do not promise an automated purge not implemented. |
| Telemetry/privacy | `web/src/main.tsx:18` enables Sentry default PII; auth context sends user ID/email. No frontend `beforeSend` redactor found. Student-content leakage was not established. | Inventory telemetry and AI/email/storage subprocessors, minimize/redact data, verify settings and access, and document retention/data use for buyer review. |
| Operations and recovery | Existing launch checklists and processing infrastructure do not prove deployment configuration, backups or restoration. | Named support owner, health/queue alerts, backup restoration rehearsal, rollback procedure, incident contacts and agreed service/recovery expectations. |
| Release gates | `.github/workflows/deploy-api.yml` builds/publishes/deploys to Production without a test step; `deploy-web.yml` has no explicit test gate. Production migration execution is not established by these workflows. Repository protection settings were not inspected. | Required backend/frontend checks, critical cross-role journey tests, SQL Server migration verification, an explicit migration/rollback procedure and post-deploy smoke tests. Service tests alone do not prove production database behavior. |
| Procurement and commercial scope | Parent subscription features do not establish district contracting, billing or onboarding readiness. | Define pilot price/unit, supported users/students/state/forms, onboarding/migration service, renewal/exit terms, support process, data-processing/security and AI-governance materials with appropriate review. Manual district invoicing is acceptable initially. |

Also verify school SSO requirements early. SSO may be an expansion feature for one pilot and a purchase prerequisite for another; do not discover this after completing the product work. Avoid promising certifications, accessibility conformance, AI accuracy or legal compliance without corresponding evidence.

Define school-sponsored family access and organization AI allowances explicitly. The individual subscription model should not unexpectedly ask a parent to pay to review a school-provided draft; budget and monitor AI usage at the organization level.

## 7. Delivery sequence and acceptance gates

### Phase A — Choose the sellable scope and repair proof gaps

- Select one launch state, one pilot school/district, target student population/document types, language requirements and supported signature procedure.
- Validate workflow assumptions with a district director, case manager and parent advocate, as the existing docs require. Include a provider and multilingual family when those workflows are in scope.
- Repair local frontend dependencies; repeat build/tests; prepare a dedicated environment using fictional student data and separate role accounts.
- Agree the source of truth, permissions and lifecycle for students, teams, meetings, shared drafts and goals before parallel feature implementation.

### Phase B — Deliver the complete demonstration

- G04 roster import/edit and G05 core team assembly.
- G01 default-editor AI/context and G02 reviewed demo-state IEP/ETR package.
- G03 parent draft review and G08 shared profile.
- G06 scheduling/deadline records, followed by G07 useful dashboard.
- Demonstrate the supported G09 completion/export path with a parent and without one.

These tracks can run in parallel after agreeing their shared models. Integration tests must use the default navigation and template editor, not a legacy route reachable only by a developer.

### Phase C — Qualify the paid pilot

- Finish supported staff lifecycle, cross-school access, account linking, multilingual/accessibility needs and procurement requirements.
- Close the operational issues in section 6 and verify real email, backup restore, record export and authorization failures.
- Complete evaluation/meeting/signature/amendment procedures promised to the pilot. Include G10 if selling full annual-cycle replacement; otherwise explicitly document the temporary supported operating boundary.
- Run real-user acceptance with prepared scenarios and resolve blocking defects. Record results and owners, not only checkbox templates.

### Phase D — Scale after measured value

- SIS sync, buyer-required SSO, two-way calendar integration, additional states and district variations.
- Expanded provider service/progress workflows and annual-review automation.
- More languages, deeper student participation and advanced meeting assistance after validating need and quality.

**Do not add to the first-demo critical path:** meeting recording/transcription, live AI interpretation, predictive family-risk scoring, individual staff productivity rankings, broad multi-state promises, or speculative integrations. These do not repair the current broken workflow connections.

## 8. A concrete school demo script

Use a fictional school with a populated roster, two case managers, a cross-building provider, an administrator and two students: one family participating online and one using offline review. Preload realistic prior assessments/progress and several clearly sourced upcoming tasks.

| Step | What the school sees | Proof |
|---|---|---|
| 1. Start at administrator home | Upcoming work and a concrete staffing exception | Each count has a source and drilldown; no invented analytics. |
| 2. Import students | Download template, upload, preview and commit | Re-upload changes one student's grade/school without duplicates or lost history. |
| 3. Assemble the team | Assign lead case manager, provider and family contacts | Sign in as provider and show the intended scoped access. |
| 4. Schedule the meeting | Choose team, time and location; show calendar | Demonstrate invitation artifact and reschedule update. |
| 5. Review student context | Strengths, family priorities, student voice and prior evidence | Private material is excluded; approved sources are identifiable. |
| 6. Draft with AI | Open the state template through normal navigation, improve a goal and an ETR narrative | Accept a source-grounded suggestion; show honest behavior with a missing baseline. |
| 7. Share and review | Send one deliberate draft snapshot; parent reads/explains and submits a question | Private parent analysis stays private; staff see only submitted input. |
| 8. Complete the meeting/document | Record reviewed change, export the approved form and attach/complete supported signature artifact | The record identifies the version and preserves its history. |
| 9. Demonstrate school-only completion | Repeat completion/export for the offline family | No portal participation blocker or misleading parent-engagement alert. |
| 10. Return to the dashboard | Completed task clears; remaining work remains visible | The dashboard reflects the actions just performed. |

Target a rehearsed 15–20 minute core story, with technical/security detail available afterward. This is a proposed demo target, not a measured capability of the current build.

## 9. Measures of readiness and value

Set numeric pilot targets with the school after measuring a baseline. Suggested measures:

- Time from roster receipt to a staff member completing the first real task.
- Import correction rate; **zero duplicate students on identical re-upload**.
- Time to create/revise a usable IEP compared with the school's existing process.
- Percentage of AI suggestions requiring factual correction, and percentage with usable source references.
- Students with a named case manager and known review/reevaluation dates; distinguish missing data from overdue work.
- Parent draft access/review/question completion, with non-digital participation recorded separately.
- Time from meeting to reviewed final record; export/signature completion for the supported workflow.
- Weekly adoption by building and role; provider task completion effort, without individual productivity rankings.
- Proven authorization denials, successful restore/export exercises and reliable invitation delivery.

**Definition of demo-ready:** the section 8 story works end to end using the default UI, realistic fictional data and the supported state forms, with clear recovery from a failed import, save or delivery.

**Definition of paid-pilot-ready:** demo-ready plus the supported record lifecycle, access/security/operations proof, migration and exit procedures, school acceptance and written commercial scope.

**Definition of broader replacement-ready:** successful pilot evidence plus the full annual-cycle workflows, jurisdiction coverage, integrations and procurement requirements actually promised to the next buyers.

## 10. Principal source map

- Product intent: `docs/personas/*.md`, `docs/journeys/*.md`; especially the indices, case manager, district director, J2/J4/J5/J6/J8.
- School operations: `docs/ops/2026-06-07-school-launch-checklist.md`, `docs/ops/2026-07-01-pilot-golden-path-runbook.md`.
- School access/roster: `api/IepAssistant.Services/Implementations/{EducatorService,OrgAccessService,StaffInviteService}.cs`; `api/IepAssistant.Domain/Entities/{SchoolStudent,StaffProfile,OrgRole,SchoolStudentAccess}.cs`.
- School surfaces: `web/src/features/educator/`, `web/src/features/district-admin/`, `web/src/features/staff-invites/`.
- Authoring integration: `web/src/config/features.ts`, `web/src/app/routes.tsx`, `web/src/features/document-authoring/`, `web/src/features/iep-authoring/`.
- Templates/export: `api/IepAssistant.Services/Implementations/{DefaultIepTemplateSeeder,TemplateResolutionService,AuthoredDocumentVersionService,AuthoredDocumentPdfDocument,IepVersionService}.cs`.
- Context/AI: `api/IepAssistant.Services/Implementations/{IepAssistService,StudentWorkspaceService,AnalysisRunService}.cs`; `web/src/features/student/`.
- Operations: `api/IepAssistant.Services/Implementations/{AuditLogger,EmailService,AccountService,AuthService}.cs`, `api/IepAssistant.Api/BackgroundServices/AccessAuditLogWorker.cs`, `api/IepAssistant.Api/Program.cs`, `web/src/main.tsx`.
- Verification suites: `api/IepAssistant.Services.Tests/`, `web/src/**/*.test.*`, `e2e/tests/`.

The next investment should make the school's complete day work: bring in its students, assemble a team, prepare a real state document with reliable assistance, involve the family deliberately, hold the meeting, and retain a usable record. That is the product story the current personas support and a school can evaluate in a purchase decision.
