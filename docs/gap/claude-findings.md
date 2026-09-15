# School Demo & Sale Readiness — Gap Analysis

**Date:** 2026-09-15
**Baseline:** `main` @ `590badd`
**Method:** Code audit of `api/` and `web/`, review of `docs/personas/` and `docs/journeys/` (2026-08-03), the design/plan history, **and a live walkthrough** of the running stack (district signup → setup wizard → roster → student → document editor) against the QA database. Backend suite: 485/485 passing.
**Companion:** `docs/gap/codex-findings.md` (independent audit, same day). This document was written without reference to its conclusions; where we agree, treat that as corroboration.

---

## 1. Verdict

**The platform can demo a parent story today. It cannot yet demo a school story a special-ed director would buy.**

The school side has real bones — district/school/staff org model, invites, scoped student access, an audit log, a template-driven document engine with immutable finalized versions and PDF rendering, MFA, and a consistent design system. That is more than a scaffold. But the seven things you listed as goals for a school are, honestly scored:

| # | Goal | Status | One-line reality |
|---|---|---|---|
| 1 | Users, roles, IEP team around a student | **Partial** | Org roles are `DistrictAdmin / SchoolAdmin / Teacher`; "team" is a per-student permission grant (`Viewer / Collaborator / Owner`), not an IEP team with roles like case manager, SLP, LEA rep |
| 2 | Student management + XLSX bulk import | **Missing** | Students can be created (4 free-text fields) — not edited, deactivated, or imported. No spreadsheet library in either stack |
| 3 | Professional dashboard, alerts, analytics | **Partial (thin)** | District home = four setup tiles (schools/staff/invites/unassigned). Teacher home = one card that says "Go to your students". Parent home = children list + account box. Nothing time- or work-aware, because nothing in the data model has a date that matters |
| 4 | Meeting scheduling + staff calendar | **Missing** | There is no meeting entity, no calendar, no deadline model anywhere. `MeetingPrepChecklist.MeetingDate` is a parent-side field on a prep artifact |
| 5 | IEP/ETR drafting, state formats, in-context AI | **Regressed** | The active editor (template engine) has **zero AI**. AI assist, chat, and "pull from student" exist only in the legacy typed editor, which is hidden behind a flag. Only a generic "Default IEP" template exists; **ETR and 504 creation dead-end** with "No document template is available" |
| 6 | Send draft to parents; parent AI review | **Missing in the active path** | There is no draft/share state (Draft → Finalizing → Finalized only). Finalized *template* documents have an API for parents but **no parent UI**. AI analysis runs only on parent-*uploaded* PDFs, never on school-authored documents |
| 7 | Student goal/bio shared with parents as AI context | **Partial** | Student workspace (strengths, interests, accommodation requests, meeting statement, AI interview) exists and is shareable. But parent-side `ChildProfile` and school-side `SchoolStudent` are separate worlds joined by a link, and no AI call sees both |

The good news: most of the distance is **connective**, not greenfield. The pieces exist in two halves that were never joined — the parent product and the school product, and inside the school product, the typed editor and the template editor.

---

## 2. The three findings that shape everything else

### F1. The template cutover orphaned the AI and the parent view

`web/src/config/features.ts` sets `IEP_AUTHORING_MODE = 'template'`. In that mode:

- **No AI.** `IepAssistController` (`api/iep-drafts/{id}/goals/{id}/assist`, `/sections/{id}/assist`, `/service-lines/{id}/assist`, `/chat`) and `IepAssistService` (Rewrite / Improve / SuggestMeasurement + draft-aware chat) are bound to `IepDraft`. `DocumentInstance` has no assist path. `grep -rl "assist\|chat" web/src/features/document-authoring` returns nothing.
- **No student context.** "Pull from student" (student workspace entries into a goal) is `iep-authoring/components/pull-from-student/*` — legacy only.
- **No structured goals.** The seeded template models goals as a `Table` field with six `Text` columns (`DefaultIepTemplateSeeder.cs:172-179`). Goals lose entity identity (`IepDraftGoal.LineageId` is gone), which forecloses per-goal AI, per-goal parent responses, and progress tracking per goal — three things every journey depends on.
- **Parents can't see the output.** `GET /api/children/{childId}/authored-versions` exists (`AuthoredDocumentVersionController.cs:76`) but the only parent UI, `SchoolIepsCard`, fetches `/api/children/{childId}/iep-versions` — the legacy table. A school that finalizes an IEP today produces a document the parent cannot open.
- **The student detail page shows both worlds.** "IEP versions" (legacy) and "Documents" (template) sit side by side on `/educator/students/:id`; a demo audience will ask which one is real.

The live walkthrough confirmed the experience: the active editor is a single long page of empty rich-text boxes (Student Profile empty even though name/grade/disability are known), followed by four empty tables, then a Finalize button. The legacy editor — tabs, AI help, pull-from-student, "Ask the assistant", autosave status — is the better product and it is the one that was turned off.

**This is the first thing to fix, and it's a decision, not just work:** the template engine is the right persistence layer for state-form fidelity, but it needs typed *block* fields (Goal block, Service block, Accommodation block) rather than generic tables, so AI and downstream features can address them. See §5, Phase 0.

### F2. There is no model of time, so there can be no dashboard, alerts, calendar, or compliance

Every persona and five of eight journeys name the timeline/deadline model as the highest-leverage missing capability. It still doesn't exist. `SchoolStudent` has no IEP date, annual review date, ETR due date, or case manager. There is no `Meeting` entity. Nothing fires on a date. `EmailService` sends invites and password resets only; there is no notification model at all (in-app or digest).

Consequence: goals 3 and 4 cannot be built as UI work. A "professional dashboard with meaningful alerts" is a rendering of dates and obligations, and there are none to render. **Build the time model before touching the dashboard**, or the dashboard will be tiles of counts again.

### F3. Roster and team are permission-shaped, not IEP-shaped

- Three org roles (`OrgRoleConfiguration.cs`: DistrictAdmin, SchoolAdmin, Teacher). The personas need at least: case manager / intervention specialist, related-service provider (SLP, OT, PT, psych), general-education teacher, school admin / LEA representative, district admin. Steph, Priya, and Dennis currently get the same sidebar and the same home.
- Per-student staff assignment is `SchoolStudentAccess { Role: Viewer|Collaborator|Owner }`. That's an ACL. An IEP team needs *what this person is on the team* ("Case manager", "SLP", "LEA rep", "Gen-ed teacher") and *who is the case manager of record* — a required field on every real IEP.
- `SchoolStudent` create form: school, first, last, free-text grade, free-text disability. No DOB (the entity has it, the form doesn't), no student ID, no case manager. No edit, no deactivate/exit/transfer (`EducatorController` has `POST students` and nothing else mutating the student).
- Parent-side `ChildProfile` duplicates name/DOB/grade/disability independently of `SchoolStudent`. After a link, there are two records of the same child that can disagree.

---

## 3. What exists and is worth keeping (inventory)

Verified against code and the live run; "exists" ≠ "production-proven".

**Org & identity**
- District self-serve signup → 4-step setup wizard (school → staff invite → done). Clean, works.
- Schools CRUD; staff invites with expiry, resend, revoke, reminder worker; deactivate/reactivate staff.
- Custom JWT auth, MFA (TOTP + recovery codes), password reset, lockout. Beta invite codes gate parent signup.
- Per-student staff access grants; roster scoping by school/access; district oversight tiles; actor-scoped audit log viewer with filters and keyset paging.
- Platform admin: user list/detail, template builder (`/admin/templates`).

**Authoring (school)**
- Template engine: `DocumentType` (IEP / Section504 / ETR) → `DocumentTemplate(StateCode?)` → published `DocumentTemplateVersion` → `TemplateSection` → `TemplateField` (Text, RichText, Date, Select, Checkbox, Table). Resolution falls back state → default. Rowversion concurrency. Seeder creates one default IEP template.
- `DocumentInstance` (ValuesJson) → finalize → immutable `AuthoredDocumentVersion` → QuestPDF render in a background worker with retry.
- Legacy `IepDraft` aggregate (sections, goals, services, accommodations, transition items, lineage ids) → `IepVersion` + PDF. AI assist ×3 kinds + chat. Autosave with flush-on-navigate.

**Parent product (Mode C — the mature half)**
- Child profiles, IEP/ETR/progress-report upload → PdfPig → Claude structuring → sections/goals; analysis runs with usage metering; meeting-prep checklist generation; IEP-to-IEP comparison; parent advocacy goals; knowledge base + IEP 101; sharing a child with another adult (Viewer/Collaborator/Owner); Stripe subscription (hidden in beta).

**Student product**
- Student invite (flips role), workspace entries (strength / interest / accommodation request / meeting statement / AI interview answer), per-entry shareable flag, educator & parent read of shareable entries.

**Platform**
- Design system: Button, Card, Table, Modal, Drawer, ConfirmDialog, Menu, Notice, Toast, Skeleton, EmptyState, PageLayout/DetailLayout, Tabs; a `guard:ux` script enforcing conventions. Looks professional in the walkthrough — muted teal/slate, serif headings, consistent spacing.
- 22 Playwright specs, 33 backend test files (485 tests), 21 web unit tests. CI deploys API/web/marketing on push to `main` — **without running any tests**.

---

## 4. Gap analysis per goal

### Goal 1 — Users, roles, and an IEP team around a student

| Have | Need |
|---|---|
| DistrictAdmin / SchoolAdmin / Teacher | Org roles: **District Admin, School Admin (LEA rep), Case Manager, Related Service Provider, General Ed Teacher**, plus a free-text title/credential (already on `StaffProfile`) |
| Per-student ACL (Viewer/Collaborator/Owner), added one at a time from a dropdown | **IEP team panel**: case manager of record (required, one), team members with *team role* (SLP, OT, psych, gen-ed, LEA rep, interpreter), parent/guardian links, student. Add by name search; bulk-assign a case manager to many students from the roster |
| Same sidebar/home for all school roles | Role-differentiated home and nav (see Goal 3). Dennis and Priya should not see an authoring-shaped shell |
| Staff invite by email + role | Keep; add **SSO (Google Workspace / Microsoft Entra)** early — nearly every Ohio district runs one of the two, and "another password" is Priya's stated abandonment trigger |

Data change: add `TeamRole` enum to `SchoolStudentAccess` (or a new `StudentTeamMember`), keep the ACL role derived from team role. Add `SchoolStudent.CaseManagerUserId`.

### Goal 2 — Student management + templated XLSX import

| Have | Need |
|---|---|
| Create student (school, first, last, grade text, disability text) | **Full student record**: student ID (district SIS ID — this is the upsert key), DOB, grade (enum), disability category (IDEA 13-category enum + Ohio list), primary language / home language, IEP date, annual review due, ETR date, next re-evaluation due, case manager, active/exited + exit reason, transfer between schools |
| No edit / no delete | Edit, deactivate/exit, transfer school, merge duplicates |
| No import | **XLSX import**: download a template workbook (headers + a "Roles & Values" sheet listing valid grades, disability codes, school names), upload → server validates row-by-row → preview with per-row errors → commit. **Re-upload is an upsert on student ID** (update changed fields, never duplicate, never silently deactivate). Store an `ImportBatch` with row outcomes so an admin can audit what changed. Same pattern for a staff import (email, role, school) — it's the same screen with a different sheet |
| — | Future SIS sync can reuse the exact upsert path; design the import service as "ingest a list of student DTOs from any source" |

Library: ClosedXML (MIT) on the API. Keep parsing server-side so the same validation serves a future API/SIS path.

### Goal 3 — Professional dashboard, alerts, analytics

Assessment of what's there (screenshots in walkthrough): the district home is a competent *setup* dashboard — four tiles (Schools, Staff, Invites, Needs attention) with clean typography. It is not an *operating* dashboard. The teacher home is a single card. The parent dashboard is a child list plus an account box.

What a buyer needs to see, by role — **all of it depends on F2 (time model) landing first**:

| Role | Home = "what needs me" |
|---|---|
| **Case manager** | Caseload queue: meetings this week, IEPs due in 30/60 days, ETRs/re-evals due, drafts in progress (with % complete), missing provider input, parent shares awaiting response. One click into the document |
| **School admin / LEA rep** | Building scan: meetings on the calendar, overdue/at-risk timelines by case manager (workload, never ranking), pre-meeting briefs to read, unsigned documents |
| **District admin** | Compliance exception board: overdue annual reviews, overdue re-evaluations, evaluations approaching 60-day clock, students with no case manager, staff adoption by building (active in last 14 days, drafts started), parent engagement (invited / accepted / reviewed) — framed as *evidence*, not surveillance |
| **Parent** | Meeting-relative: "Jordan's annual review is in 12 days — here's the draft, here's your prep." Then documents, then progress |

Analytics that are cheap once the time model exists: on-time %, days-to-finalize, goals per IEP, % goals with baseline, parent review rate, AI-assist acceptance rate. Charts via a small inline SVG/Recharts set — do **not** add a heavy dashboard library.

Polish items from the walkthrough: sidebar footer clips the user's name behind the Sign Out row; educator home repeats the district name as both H1 and card H2; no favicon/brand in tab title beyond "IEP Advisor".

### Goal 4 — Meeting scheduling + staff calendar

Nothing exists. Build:

- `Meeting { SchoolStudentId, Type (Annual IEP, ETR/Eligibility, Amendment, Transition, Manifestation), ScheduledAt, Duration, Location/Video link, Status (Proposed, Scheduled, Held, Continued, Cancelled), DocumentInstanceId?, CreatedBy }`
- `MeetingParticipant { MeetingId, UserId? / ExternalName+Role, TeamRole, Required, Invited, Attended, Excusal }` — attendance is a compliance record (Dennis's "participant record written from memory afterward").
- Invitation flow: propose date(s) → parent notified (email + in-app) → confirm. Ohio's PR-02 parent invitation is a form; generating it from this record is a fast follow.
- **Staff calendar page**: month/week view of the meetings I'm on, plus my caseload deadlines. **ICS feed per user** (subscribe in Google/Outlook) — cheaper and more used than an in-app calendar, and it's what school staff actually live in. In-app "Upcoming" list on the home page.
- Reminders: T-7, T-1, T-1h via email; in-app notification centre.

### Goal 5 — IEP & ETR drafting, state formats, in-context AI

This is the product's centre of gravity and the largest gap. In order:

1. **Unify on the template engine, but give it typed blocks.** Add `FieldType.GoalBlock`, `ServiceBlock`, `AccommodationBlock` (repeating structured groups with stable per-row lineage ids). Migrate the seeded template's Table fields to blocks. Port from the legacy editor: assist popover (Rewrite / Improve / SuggestMeasurement), draft-aware chat, pull-from-student, autosave status, section tabs/navigator. Then retire the typed editor and its routes.
2. **Never blank.** New document pre-fills from the student record (name, DOB, grade, disability, school, case manager, team), the prior finalized IEP (goals carried forward with "carried from 2025 IEP" provenance), and the ETR (present levels). This is the single feature Steph will judge the product by.
3. **AI grounded in the student, with citations.** Every assist call gets a student context pack: profile, prior IEP goals + progress, ETR findings, student workspace entries, parent input. Output must cite which source it drew from; "no baseline data available" over an invented number. The system prompts in `IepAssistService` are a fine start but currently see only the draft.
4. **Ohio form fidelity.** The target state is Ohio (district state OH, "ETR" is Ohio's term). Author templates for **PR-07 (IEP)** and **PR-06 (ETR)** with the real section list (ETR: referral, planning, individual evaluator reports, team summary, eligibility determination; IEP: future planning, special-instructional factors, profile, extended school year, postsecondary transition, measurable annual goals, specially designed services, transportation, nonacademic/extracurricular, general factors, LRE, testing, meeting participants, signatures). PDF layout should approximate the ODE form so a director recognises it. Seed them as `StateCode = "OH"` templates.
5. **District-level template management.** Templates are platform-admin only today (`/admin/templates`); districts need to view and lightly customise (add a district section, set defaults) without calling you.
6. **Compliance checks, advisory not blocking**: required sections empty, goals without baseline/measurement, services without frequency, transition missing for 14+, meeting participants incomplete. Surface as a sidebar checklist on the editor.
7. **Amendment path**: new draft from a finalized version, diff shown, finalize as amendment.

### Goal 6 — Parent interaction: share draft, review, AI explanation and questions

| Have | Need |
|---|---|
| Finalize → parent sees (legacy path only) | **Share state on the draft**: Working → Shared → Proposed → Finalized. One deliberate "Share draft with family" action; re-share produces a "what changed" summary. The journeys decided this on 2026-08-03; it's the precondition for a parent story in the demo |
| Parent analysis on uploaded PDFs | **AI analysis on a school-shared draft**: add `AnalysisSourceType.AuthoredDocument` and run the existing analysis pipeline over the shared draft's content. Plain-language "what this means" per section and per goal; "ask a question about this draft" chat with citations to the draft |
| — | **Per-item parent responses**: on each goal/service, "I agree / I have a question / I'd like a change" + free text, visible to the case manager in a converge view before the meeting. This is what turns the meeting into a conversation and produces the participation evidence Karen wants |
| — | Parent UI for template finalized versions (route + card) — a one-day fix that unblocks the end-to-end story immediately |
| — | Post-meeting summary to the family (plain language) and the finalized PDF |

Privacy rule to keep visible in UI: parent prep notes and AI analysis are private to the parent; only explicit responses go to the school. Both P1 and P7 require this.

### Goal 7 — Student goal & bio details, shared with parents, as AI context

| Have | Need |
|---|---|
| Student workspace (5 entry kinds, shareable flag), read by educator/parent | **Unified student context** that every AI call reads: profile, strengths/interests (student- and parent-authored), current goals with progress, accommodations, ETR summary, parent concerns/vision. Store as a queryable `StudentContext` view, not a prompt string |
| Parent `ChildProfile` and school `SchoolStudent` separate | After a link, show the parent the school record as the source of truth for identity fields; keep parent-authored fields (concerns, home observations, vision) as the parent's contribution. Parent adds "About Jordan at home" once; it flows into present levels and AI suggestions |
| Goals only exist inside a document | **Goals as first-class entities with cross-year identity** (J8's data-model decision): created in an IEP, carried forward, revised, met, retired. Enables progress charts, stale-goal detection, and "same goal, third year" flags |

---

## 5. Recommended additions to your list

Ordered by how much they change a director's buying decision.

1. **Compliance timeline model** (F2). Annual review, 3-year re-evaluation, evaluation 60-day clock from consent, transition-by-14, age-of-majority. This is the personas' unanimous #1 and the prerequisite for goals 3 and 4.
2. **Demo data seeder.** A `dotnet run -- seed-demo` that creates "Maple Ridge Local Schools" with 3 buildings, 8 staff across roles, 40 students with realistic names/grades/disabilities, 15 finalized IEPs with goals, meetings across the next 60 days, two parent accounts with shared drafts, one student account. You cannot demo a queue-shaped dashboard on a blank district, and you should never demo on the QA database with a real child's name in it (I saw one).
3. **Prior Written Notice + consent + signatures.** As system of record, PR-01 (PWN), PR-05 (consent for evaluation), and the IEP signature page are in scope. Start with a signature capture on finalize (typed name + timestamp + IP) and printable signature lines; real e-sign later.
4. **Notifications.** In-app centre + email, one digest per person per day, trigger-based only (meeting scheduled, draft shared, response received, deadline at 30/14/7). No engagement noise.
5. **Procurement pack as product surfaces**: a `/trust` page (data handling, retention, deletion, no model training, sub-processors, AI governance: what AI touched, who approved), an accessibility statement, a DPA template, and a data-export-for-district action. Karen's security review will ask in writing; having it on the site shortens the sale by weeks.
6. **SSO** (Google/Microsoft) and a magic-link path for related-service providers.
7. **Section 504** template — the document type is already seeded; a template makes it real and widens the buyer conversation.
8. **Progress data capture** for service providers (60-second entry against a goal, trajectory vs. target) — the biggest post-pilot differentiator, but not needed for the first demo.
9. **Marketing site school track.** `marketing/index.html` is parent-only ("Understand Your Child's IEP"). A director who Googles you finds no school product.
10. **Ship hygiene**: CI runs `dotnet test` and `vitest` before deploy; retire `README.md` / `PLAN.md` / `PROGRESS.md` at repo root (all March, all wrong now — the README still references a mobile app); resolve the five open P2 todos from the last review; remove the legacy typed editor once ported.

---

## 6. Personas & journeys — review

These are unusually good design inputs. Three things to say:

**What holds up.** The four-way split of "Educator" (Steph / Priya / Dennis / Karen) is exactly right and the code confirms the problem it names — one role, one shell. The three operating modes (both sides / school-only / parent-only) are the correct frame, and Mode B ("the school must be able to finish and export with zero family participation") is a sale requirement, not a fallback. The "draft is never blank" and "AI proposes, Steph disposes, every claim cited" rules should be printed and pinned above the editor work.

**What to correct.**
- The "Gap vs. today" tables were written 2026-08-03. Since then the template engine merged (PR #21) and **made several gaps worse** — J4's "Educator authoring workspace + AI" went from *partial* to *absent in the default path*. The journey docs should be re-ledgered after Phase 0 below.
- J2 says "Bulk roster import (CSV)". Make it XLSX with a downloadable template and upsert semantics (your goal 2) — CSV re-uploads without a stable key create duplicates, which is the failure districts remember.
- P8 (platform admin, "structure not content") is sound, but note that the existing `/admin/users/:id` and the QA database give an internal admin full content access today. That's a security-review answer you'll need before real student data.
- J6's live bilingual meeting support and J7's voice capture are the right vision and the wrong order. Nothing bilingual should be attempted before the time model and the authoring loop close; when it is, it should be Spanish, glossary-constrained, document-level first.

**Open decisions that block build work** (from the docs, still undecided; you should decide these before Phase 1):
1. **Ohio only for the first sale?** Yes is the right answer; it collapses the "state form variance" question to two forms.
2. **Team roles: hat or job?** Model LEA rep as a per-meeting role (a hat), case manager as a per-student field (a job).
3. **District policy switch to disable family draft visibility.** Build it (one boolean on `District`); some districts will demand it and it costs nothing now.
4. **Are goals entities?** Yes, or J7/J8 never happen.

---

## 7. Roadmap to a sellable demo

Sizes are rough person-weeks for one strong full-stack dev with the current codebase; phases are sequential because each depends on the last.

### Phase 0 — Repair the spine (≈2–3 wks) — *blocks everything*
- Typed blocks in the template engine (GoalBlock/ServiceBlock/AccommodationBlock) with lineage ids; migrate default template.
- Port AI assist, chat, pull-from-student, section navigator, autosave indicator into the document editor. Retire the typed editor.
- Pre-fill new documents from the student record.
- Seed **OH PR-07 IEP** and **OH PR-06 ETR** templates (structure first, PDF layout second); a minimal 504.
- Parent route + card for authored versions. Student detail page shows one "Documents" concept.
- Demo data seeder.
- CI runs tests.

**Exit test:** a case manager creates an IEP for a seeded student, the profile is pre-filled, AI proposes a measurable goal grounded in the ETR, they finalize, and the linked parent opens the PDF.

### Phase 1 — Students, staff, team (≈2 wks)
- Full student record + edit/exit/transfer; org roles expanded; case manager of record; IEP team panel with team roles; bulk assign from roster.
- XLSX import (students, then staff): template download, validate, preview, upsert-by-ID commit, import history.
- Role-differentiated nav.

### Phase 2 — Time (≈2–3 wks)
- Compliance dates on the student (IEP date, annual due, ETR date, re-eval due) — set on finalize, editable, imported from the spreadsheet.
- `Meeting` + participants + scheduling flow + ICS feed + calendar page.
- Notification model (in-app + email digest) with the first five triggers.

### Phase 3 — Dashboards on real data (≈2 wks)
- Case manager queue home; school admin building scan; district compliance board with adoption + engagement; parent meeting-relative home.
- Analytics tiles + 3–4 charts. Fix the sidebar footer clip and heading duplication while in there.

### Phase 4 — Parent loop (≈2–3 wks)
- Draft share state + "what changed"; per-item parent responses; converge view for the case manager.
- AI analysis + plain-language explanation + Q&A on the shared draft (new `AnalysisSourceType`).
- Unified student context feeding every AI call; parent "about my child" contribution.
- Post-meeting family summary.

### Phase 5 — System-of-record credibility (≈2–3 wks)
- PR-07/PR-06 PDF fidelity; PWN (PR-01) generation; consent + signature capture on finalize; amendment path; district data export.
- `/trust` page, accessibility statement, DPA template; SSO.
- Marketing school track.

After Phase 3 you can run a convincing 20-minute demo. After Phase 5 you can sign a bounded paid pilot (one district, one building, Ohio, IEP + ETR) without a compliance objection ending the conversation.

---

## 8. The demo you're building toward (script)

1. **District admin** logs in → compliance board: "3 annual reviews due in 30 days, 1 re-evaluation overdue, 2 students without a case manager." Click the overdue one.
2. **Roster** → "Import students" → drop the XLSX the district exported from their SIS → preview shows 38 updated, 2 new, 0 errors → commit. Show the audit log line.
3. **Student page** — Jordan Ellis: profile, IEP team (case manager, SLP, gen-ed, LEA rep, mom), timeline (IEP dated 10/2025, annual due 10/2026, ETR 2024, re-eval 2027), documents, upcoming meeting.
4. **Schedule the annual review** → pick a date → parent notified → it lands on the SLP's calendar (show the ICS in Google Calendar).
5. **Case manager** opens "New IEP" → pre-filled from last year and the ETR → goals carried forward with provenance → AI: "Rewrite this goal to be measurable using the ETR baseline" → accept → compliance sidebar goes green → **Share draft with family**.
6. **Parent** (phone) gets the email → opens the draft → "Explain this goal" → asks "Is 20 minutes of speech a week normal for a 7th grader with this profile?" → marks one goal "I have a question."
7. **Case manager** sees the response in the converge view before the meeting → LEA rep opens the two-minute pre-meeting brief.
8. **Finalize** → signature lines → PR-07-shaped PDF → parent receives it; district dashboard on-time % ticks up.

Every step above maps to a phase in §7; none of it is possible today past step 1's login.

---

## 9. Appendix — smaller findings from the walkthrough and code

- Running the API locally on `https://localhost:7000` fails on this Mac: macOS Control Center (AirPlay) holds port 7000. The e2e `.env` and runbook already assume 7200; `vite.config.ts` still proxies to 7000. Align the config.
- `Grade Level` and `Disability Category` are free text on student create — will fragment analytics immediately. Enum them.
- `KnowledgeBase` is read-only with no seed in code; whatever populated the QA DB isn't reproducible from the repo.
- `DocumentTemplate` uniqueness is `(StateCode, DocumentTypeId)` — one template per state per type, so no district variants without a schema change. Fine for Ohio pilot; note it.
- `IepVersion`/`AuthoredDocumentVersion` immutability is application-level; the design doc's DB-trigger backstop is still open.
- Subscription/Stripe is parent-side only; there is no district billing/contract/seat model. Not a demo blocker, but the pricing decision (per student vs. per seat) shapes what adoption metrics the dashboard shows.
- The "Support" sidebar link goes to a Google Form.
- Marketing `<title>` and the app's brand line ("Navigate with confidence") are parent-facing; the school product needs its own line.
