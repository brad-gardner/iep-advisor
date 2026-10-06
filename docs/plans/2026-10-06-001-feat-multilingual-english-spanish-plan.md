---
title: "feat: Multilingual site — English + Spanish, page by page"
type: feat
status: active
date: 2026-10-06
design: docs/designs/2026-10-06-multilingual-english-spanish-design.md
slicing_approach: vertical
---

# feat: Multilingual site — English + Spanish, page by page

## Overview

Every page, shared component, server message, email, notification, PDF and AI response follows the user's language. English and Spanish ship together. The work is split into 7 vertical phases, one PR each.

- Each phase converts a set of pages end to end: strings go into translation files, Spanish is written for them, dates are localized, and a lint guard is turned on.
- Each phase leaves those pages fully working in Spanish.
- Phase 1 builds the foundation: i18n setup, language detection plus the account preference, the switcher, the shell, sign-in pages and request culture.

## Problem Statement

The site is English-only:

- **Web:** about 420 components (≈48k lines) contain English literals, and there's no i18n library. `index.html` hard-codes `lang="en"`.
- **API:** messages, 13 email kinds, notification text, 2 PDF generators and every AI prompt are English.
- **Language data:** schools record a student's `HomeLanguage`, but nothing uses it.

The school-sale gap audit (`docs/gap/combined-findings.md` C11) lists language access for families as needed. Spanish is the most common non-English home language in US public schools.

## Proposed Solution

This follows the approved design (`docs/designs/2026-10-06-multilingual-english-spanish-design.md`).

- **Web:** react-i18next, with one namespace per feature in `web/src/locales/{en,es}/<ns>.json`. Keys are typed, and English is bundled while Spanish is lazy-loaded.
- **Language resolution:** account `preferredLanguage` → pre-login choice → browser `navigator.languages` → `en`. The first sign-in with no saved preference stores the resolved language.
- **API:** `RequestLocalization` (saved preference → `Accept-Language`), `.resx` + `IStringLocalizer` for messages, and the recipient's culture for emails, notifications and PDFs.
- **AI:** a shared response-language line for Spanish requesters. Generated artifacts record their language, and the document parsers are untouched.
- **Spanish quality:** drafted by Claude following `docs/i18n/glossary-es.md` (neutral Latin American, formal "usted", US Dept. of Ed IDEA terminology) and tracked as "needs native review" per namespace in `docs/i18n/README.md`.

**Decisions (2026-10-06):**

- Scope: everything, phased.
- Beyond UI: AI output, emails, PDF exports and API messages also follow the language.
- Browser detection plus the account preference.
- Knowledge-base/IEP 101 article bodies: follow-up, not in this plan.
- Vertical phases approved as outlined.

## Design direction

- **Mode:** operate. The site already exists; translation must not change layout or visual language.
- **Visual system:** preserve the existing tokens and components (`web/src/index.css` / Tailwind brand tokens, `web/src/components/ui/*`).
- **Screens and states:** every converted page in default, empty, loading, error and success states, in both languages. Spanish runs about 20–30% longer than English, so check buttons, nav labels, badges, table headers, the completeness strip and tabs for wrapping or clipping.
- **Language switcher:**
  - A compact `Select` or menu labeled "Language / Idioma" in the auth layout footer and the sidebar footer.
  - Each option shows its own name ("English", "Español").
  - It is keyboard and screen-reader accessible, and switching updates `<html lang>`.
- **Follow:** existing `Select`/`Menu` components, `Notice` for "Generated in English" banners, and the existing toast patterns.
- **Avoid:**
  - flag icons for languages;
  - truncating translated labels with ellipses to make them fit (wrap instead);
  - mixed-language screens on converted pages.
- **Assumptions:** the switcher sits in the sidebar footer and auth footer. There is no top-bar redesign.
- **Tooling:** impeccable detector on changed UI files each phase.

## Technical Approach

### Architecture

**Web**

- `web/src/lib/i18n/`:
  - `index.ts`: i18next init and resources (English static import; Spanish through `import.meta.glob('../../locales/es/*.json')` with a resources-to-backend loader).
  - `detect.ts`: resolution order plus normalizing `es-*` to `es`.
  - `types.d.ts`: `CustomTypeOptions` from `en`.
  - `format.ts`: `Intl` helpers keyed on `i18n.language`.
  - `language-switcher.tsx`.
- Namespaces: `common` (shell, shared UI, enum labels, generic errors) plus one per feature folder (`auth`, `children`, `home`, `iep-documents`, …).
- `AuthProvider` handles the preference:
  - after `/me`, it applies `user.preferredLanguage`;
  - if that is null, it PUTs the resolved language;
  - a language change PUTs `preferredLanguage`.
- `lib/api-client` (axios) adds an `Accept-Language` interceptor.
- Label maps become helpers, e.g. `disabilityCategoryLabel(code)` → `t('common:disabilityCategory.' + code)`. Duplicated local maps (`MEETING_TYPE_LABELS` in three files, `SECTION_LABELS` in four) are collapsed into one helper each.
- `test/setup.ts` initializes i18n synchronously with `en`, so existing tests keep passing. The helper `renderInSpanish(ui)` is for Spanish tests.
- Lint:
  - `eslint-plugin-i18next` `no-literal-string` (mode `jsx-only`, plus attributes `aria-label|title|placeholder|label|alt`) is enabled per converted folder in `eslint.config.js`.
  - Test files are excluded.
  - It becomes global in Phase 7.

**API**

- `User.PreferredLanguage` (`nvarchar(10)` nullable) with migration `AddUserPreferredLanguage`.
  - It is added to `UserDto`, `UpdateProfileRequest` and the `PUT /api/auth/me` handler, validated against `SupportedLanguages = ["en","es"]`.
- `Program.cs`:
  - `AddLocalization(o => o.ResourcesPath = "Resources")`;
  - `UseRequestLocalization` with a custom first provider that reads the signed-in user's `PreferredLanguage` (from claims, or one cached lookup), then `AcceptLanguageHeaderRequestCultureProvider`.
- `IepAssistant.Services/Resources/Messages.resx` + `Messages.es.resx` (plus `Emails.*`, `Pdf.*`, `Ai.*` as phases reach them), read through `IStringLocalizer<T>`.
- `CultureScope.For(string? language)` is a disposable that sets `CurrentUICulture`/`CurrentCulture`. Workers, email and notification fan-out and PDF generation use it.
- `ResponseLanguage.SystemLine(CultureInfo)` returns "" for `en`, or the Spanish instruction for `es`. It is applied in every non-parser prompt builder.
- Each generated AI artifact gets a `Language` column (`AnalysisRun`, meeting-prep, meeting summary/brief, advocate thread, draft explanation/questions, progress-report analysis) via migration.
- PDF services take a `CultureInfo`, and blob and cache keys include the language.

### Implementation Phases

Each phase converts its pages and is done when:

1. no literal UI strings remain (the lint rule is on for those folders);
2. `en` and `es` namespaces are complete (the parity test passes);
3. dates and numbers use the active locale;
4. Spanish render tests cover each page (headings and primary controls are Spanish, and no raw `ns:key` is visible);
5. affected API messages, emails and AI for those pages are localized;
6. CI is green: `tsc -b`, `test:types`, vitest, ESLint ≤36 baseline, `dotnet test`;
7. the impeccable detector is clean on changed UI;
8. any migrations are applied to QA before merge (see memory note: QA deploy doesn't migrate).

#### Phase 1: Foundation — sign in and navigate in Spanish

- Web i18n setup (`lib/i18n/*`), the parity test, typed keys, the test setup and `renderInSpanish`.
- Detection and the account preference:
  - `User.PreferredLanguage` with its migration, DTO and validation;
  - AuthProvider apply/persist;
  - the pre-login localStorage choice;
  - `?lang=` query support on public pages.
- `LanguageSwitcher` in `auth-layout.tsx` and `sidebar.tsx`, a Language field on `features/auth/components/profile-page.tsx`, and dynamic `<html lang>`.
- `common` namespace:
  - sidebar's 18 nav labels plus the open/close labels;
  - `main-layout`;
  - shared UI defaults in `components/ui/*` (confirm-dialog, pagination, modal/drawer, spinner, table, page-header, pdf-viewer, rich-text toolbar, toast, empty-state, notice, charts);
  - route spinner fallbacks in `app/routes.tsx`;
  - generic `api-error.ts` fallbacks.
- `lib/format-date.ts`, `lib/relative-time.ts` and `meetings/lib/meeting-time` use the active locale. Remove the 5 `'en-US'` literals.
- Auth pages (`features/auth`, 17 files): login, register, forgot/reset password, MFA verify/setup, magic link, profile, plus the staff and student accept-invite and the account cancel-deletion screens.
- API:
  - `AddLocalization` + `RequestLocalization` + preference provider;
  - `Messages.resx`/`.es.resx` covering `AuthService`/`AuthController` messages;
  - the password reset and magic link emails in the recipient's language.
- `docs/i18n/glossary-es.md` (special-ed terminology) and `docs/i18n/README.md` (process: adding a key, review status per namespace).
- **Checkpoint:** with a Spanish browser:
  - the login page is in Spanish;
  - signing in stores `es`;
  - nav and profile are in Spanish;
  - a password reset email arrives in Spanish;
  - switching to English persists across reload and devices.

#### Phase 2: Parent core

- `features/home` parent home and dashboard sections.
- `features/children`: list, detail shell and tabs overview/goals, and the child form (`child-profile-options.ts` labels through `common`).
- `features/onboarding`, `features/notifications`, `features/subscription`, `features/child-links`, `features/sharing` (accept-invite, redeem-invite).
- Knowledge-base and IEP 101 page chrome only; article bodies stay English and are labeled "Available in English".
- Enum label helpers used here: grade, disability, status.
- API: messages from ChildProfile, ChildLink, Sharing, Subscription and Notification services.
- **Checkpoint:** a Spanish parent can onboard, add or edit a child, view the dashboard, manage notifications and their subscription, and accept a share link.

#### Phase 3: Parent documents and AI

- Pages:
  - `features/iep-documents` (viewer, list, analysis tab);
  - `features/etr-documents`;
  - `features/analysis`;
  - `features/iep-comparison`;
  - `features/iep-versions` (including authored versions);
  - `features/progress-reports`;
  - `features/goals`, `features/advocacy-goals`;
  - `features/meeting-prep`, `features/journal` (`lib/copy.ts`);
  - `features/advocate` (`lib/copy.ts`, `state-hint`);
  - `features/shared-drafts`, `features/draft-sharing` (parent side);
  - `features/meetings` (RSVP page, family summary);
  - `features/student`.
- Label maps: `SECTION_LABELS` ×4 → one helper; meeting type/status; ETR evaluation/document state; journal tags; draft response kinds.
- AI in Spanish: Advocate, meeting prep, draft questions and explanations, meeting summary (family), analysis synthesis, progress-report analysis, student workspace.
  - `ResponseLanguage` helper, plus `Language` columns via migration `AddAiArtifactLanguage`.
  - A "Generated in English/Spanish" `Notice` when the artifact language differs from the viewer's.
  - Canned AI strings (disclaimer, cap and unavailable messages, "New conversation") move to resources.
  - Parsers are untouched, verified by test.
- **Checkpoint:**
  - a Spanish parent uploads an IEP, the analysis comes back in Spanish, and the extracted IEP text is unchanged;
  - meeting prep and the Advocate answer in Spanish;
  - an English co-parent sees the "Generated in Spanish" notice.

#### Phase 4: Emails, notifications and parent-facing server messages

- `EmailService` email kinds in the recipient's language:
  - share, school-link, student and beta invites, meeting invitation/update/cancel (plus .ics text), account-deletion link, generic notification, digest;
  - `Emails.resx`/`.es.resx`;
  - pre-account invitees get the sender's language and links with `?lang=`.
- Notification creation (DraftSharing, DraftResponse, Meeting, MeetingSummary, MeetingBrief, MeetingReminder, EvaluationCase, Export, Digest, AuditIntegrity services) renders the title and body in each recipient's culture via `CultureScope`.
- Remaining parent-reachable `ServiceResult` messages are localized.
- **Checkpoint:** with one English and one Spanish recipient, each email kind and notification arrives in the right language (API tests per kind, plus one manual send on QA).

#### Phase 5: Student and school staff

- Pages:
  - `features/home` staff home;
  - `features/educator` (students list and detail, team, edit forms, label maps grade/disability/status/exit/team role/attention);
  - `features/calendar`, `features/meeting-brief`, `features/meetings` (staff);
  - `features/document-authoring` (52 files: editor, section cards, goal/service editors, completeness, signatures, assist);
  - `features/evaluation`, `features/obligations`, `features/family-contact`, `features/contributions`, `features/draft-sharing` (staff side).
- AI in Spanish for staff who choose it: document assist (`AssistPrompts`), IEP assist, meeting brief.
- API: Educator, Meeting, Document, Evaluation and Obligation service messages.
- **Checkpoint:** a staff member in Spanish can find a student, edit and finalize a document, schedule a meeting and see the brief. District content (template labels, values) stays as written.

#### Phase 6: District and platform admin

- Pages:
  - `features/district-admin` (setup wizard, schools, compliance, activity);
  - `features/staff-invites`, `features/roster-import` (wizard step labels);
  - `features/exports`;
  - `features/admin` (users, templates including `document-semantics.ts` labels, notifications, email, audit including `ACTION_LABELS`).
- API: District, StaffInvite (31 messages), RosterImport, Export and Admin service messages; the staff invite and invite-expiring emails.
- **Checkpoint:** the district setup wizard and roster import complete in Spanish, and platform admin pages render in Spanish.

#### Phase 7: PDFs, remaining server messages, global guard

- `IepVersionPdfDocument`/`AuthoredDocumentPdfDocument` labels through `Pdf.resx`. Workers and services take a culture, and blob/cache keys include the language. Spanish users download Spanish-labelled PDFs; content stays as written.
- Sweep the remaining literal `FailureResult` strings (a grep check: no unlocalized literals in `Services/Implementations`).
- `no-literal-string` becomes global (non-test `web/src`). `docs/i18n/README.md` coverage reaches 100%.
- Final Spanish walkthrough across all audiences. Record review status.
- **Checkpoint:**
  - the global lint rule is clean;
  - Spanish and English PDFs are both downloadable for the same version;
  - no remaining English on any page in Spanish mode, except user or district content.

## Alternative Approaches Considered

- **react-intl (FormatJS):** strong ICU support, but more verbose call sites and no lazy namespace story out of the box. Rejected.
- **Lingui:** small runtime, but it needs a Babel/SWC macro and an extraction step in the Vite build. Rejected as extra build complexity.
- **Error codes from the API, translated on the web:** cleaner separation, but it would mean rewriting about 700 results and 81 call sites. Rejected in favor of server-side `.resx`.
- **Browser-only preference (localStorage):** no migration, but emails and AI couldn't know the language. Rejected; detection is still the first-visit default.
- **Machine translation at runtime (an API):** costs per view, quality varies, and it leaks content to a third party. Rejected; translations are static files.

## System-Wide Impact

### Interaction Graph

- **Language change in the switcher:**
  1. `i18n.changeLanguage`;
  2. `<html lang>` updates;
  3. the Spanish namespaces load;
  4. AuthProvider `PUT /api/auth/me`;
  5. axios `Accept-Language` updates for later calls;
  6. later emails, notifications and AI use the new language.
- **Sign-in with no preference:** `/me` returns null → AuthProvider PUTs the detected language → `User.PreferredLanguage` is set.
- **An AI request:** RequestLocalization sets `CurrentUICulture` → the prompt builder appends the `ResponseLanguage` line → the artifact is saved with `Language`.
- **Background work:** the worker loads the recipient → `CultureScope.For(recipient.PreferredLanguage)` → resources resolve in Spanish.

### Error & Failure Propagation

- **Missing Spanish key at runtime:** i18next falls back to `en`. That is visible but never blank. The parity test blocks merges with missing keys.
- **Spanish namespace fails to load (network):** fall back to `en` and log to Sentry.
- **Invalid `preferredLanguage` on PUT:** 400 with a localized validation message. An unknown value in the DB is treated as `en`.
- **AI:** if the model answers in the wrong language, nothing breaks. The artifact language records what was requested.

### State Lifecycle Risks

- **Notifications stored before Phase 4 stay English.** Acceptable, and stated in the release notes.
- **PDF cache keys change in Phase 7:** existing cached English PDFs are not served for Spanish requests, and English keys stay compatible (default `en`).
- **AI artifacts created before the `Language` column:** null means `en`.

### API Surface Parity

- `GET`/`PUT /api/auth/me` gain `preferredLanguage`. Update the Bruno collection.
- Every endpoint honors `Accept-Language` and the saved preference for `message`.
- PDF download endpoints take the language from the request culture.

### Integration Test Scenarios

1. A Spanish browser with no account: login page in Spanish → register → `/me` shows `es`.
2. An English parent shares a child with a new Spanish-speaking co-parent: the share email is in English (the sender's language) with `?lang=en`, and after they choose Spanish their notifications arrive in Spanish.
3. A Spanish parent runs the IEP analysis: the synthesis is Spanish, `AnalysisRun.Language = es`, and the extracted IEP text is byte-identical to an English run.
4. A staff member (English) finalizes an IEP, and a Spanish parent downloads the PDF: Spanish labels, English district content.
5. A server validation error on child update is returned in Spanish when `Accept-Language: es`.

## Acceptance Criteria

### Functional Requirements

- [ ] A first visit uses the browser language (Spanish → Spanish; anything else → English).
- [ ] The language choice is saved to the account and follows the user across devices. The switcher is on the auth pages, the sidebar and Profile.
- [ ] Every page in all 5 audiences renders fully in Spanish when Spanish is active, except user- and district-authored content and KB article bodies.
- [ ] Dates and numbers are formatted for the active language.
- [ ] API messages, emails (all kinds), notifications, PDFs and AI responses follow the user's or recipient's language as designed.
- [ ] AI artifacts record their language; viewers in another language see a notice.
- [ ] IEP/ETR extraction output is unchanged by language.

### Non-Functional Requirements

- [ ] English bundle size grows by no more than about 15% gzip. Spanish loads lazily per namespace.
- [ ] No layout clipping or overflow in Spanish at 1366×768 and 400px width on converted pages.
- [ ] Accessibility: `<html lang>` matches the language; the switcher has a label; translated `aria-label`s.
- [ ] No user text or PII is sent to any translation service (translations are static).

### Quality Gates

- [ ] Each phase meets its definition of done above.
- [ ] `docs/i18n/README.md` lists every namespace with its review status. Spanish is marked "needs native review" until reviewed.
- [ ] Phase 7: the global `no-literal-string` rule is clean.

## Success Metrics

- 100% of `en` keys present in `es` (parity test).
- 0 literal JSX strings in non-test `web/src` (lint).
- 0 literal `FailureResult` messages in `Services/Implementations` (grep check).
- After launch: the share of active users with `preferredLanguage = es`, and Spanish-user retention compared with English.

## Dependencies & Prerequisites

- npm: `i18next@^26`, `react-i18next@^17`, `i18next-browser-languagedetector@^8`, `i18next-resources-to-backend`, and the dev dependency `eslint-plugin-i18next`.
- EF migrations in Phases 1 (`AddUserPreferredLanguage`) and 3 (`AddAiArtifactLanguage`). Apply them to QA manually before merge.
- A native Spanish reviewer (ideally special-ed literate) before marketing Spanish. This does not block shipping.

## Risk Analysis & Mitigation

| Risk | Mitigation |
|---|---|
| Spanish special-ed terminology is wrong or inconsistent | Glossary file used by every phase; review-status tracking; native review before marketing |
| Huge diffs are hard to review | Vertical phases, one PR each; mechanical extraction kept separate from behavior changes within a phase |
| Tests break from text changes | Tests stay English by default; Spanish tests added, not substituted |
| Layout overflow from longer Spanish | Design direction says wrap, not truncate; the detector plus a manual pass at 1366×768 and 400px each phase |
| The AI ignores the language instruction | The instruction goes in the system prompt with explicit acronym rules; artifacts record the requested language; spot-check in QA |
| Translating the parsers corrupts extraction | Parsers explicitly excluded; a test asserts no language line in parser prompts |
| Lint baseline gate breaks | The per-folder rule only covers converted folders, so it starts at 0 there |

## Future Considerations

- Knowledge-base and IEP 101 article translation: a `Language` column and Spanish articles (follow-up todo).
- More languages (Vietnamese, Arabic, Chinese, Somali are common US home languages). The setup supports them by adding `locales/<lng>`. Arabic needs RTL layout work.
- Using `SchoolStudent.HomeLanguage` to default family invites to the family's language.
- "Translate this document" for district content (IEP text) with clear labeling. Out of scope; has legal implications.

## Sources & References

- **Design:** `docs/designs/2026-10-06-multilingual-english-spanish-design.md` (approved 2026-10-06).
- **Gap audit:** `docs/gap/combined-findings.md` §C11 (language).
- **Code:**
  - `web/src/app/routes.tsx`, `web/src/components/layouts/sidebar.tsx`
  - `web/src/features/auth/stores/auth-context.tsx`, `web/src/lib/format-date.ts`, `web/src/lib/api-error.ts`
  - `api/IepAssistant.Services/Implementations/EmailService.cs`, `api/IepAssistant.Services/Models/ServiceResult.cs`
  - `api/IepAssistant.Services/Implementations/{AdvocatePrompts,DraftPrompts,DraftPromptBuilder,PromptText}.cs`
  - `api/IepAssistant.Services/Implementations/{IepVersionPdfDocument,AuthoredDocumentPdfDocument}.cs`
- **Learnings:**
  - `docs/solutions/logic-errors/2026-10-06-free-text-to-dropdown-legacy-values-and-not-set-clear.md`: display labels vs. stored values.
  - Memory: QA deploy doesn't run migrations.
- **External:**
  - i18next: https://www.i18next.com/
  - react-i18next: https://react.i18next.com/
  - TypeScript typing: https://www.i18next.com/overview/typescript
  - ASP.NET Core localization: https://learn.microsoft.com/aspnet/core/fundamentals/localization
  - eslint-plugin-i18next: https://github.com/edvardchen/eslint-plugin-i18next
