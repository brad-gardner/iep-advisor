---
module: "AI services (analysis runs, progress-report analysis, meeting summary, advocate, draft Q&A)"
date: "2026-10-06"
problem_type: logic_error
component: service_object
symptoms:
  - "With 'respond in Spanish', the model could return 'rojo' or 'abordado' instead of 'red' or 'addressed'. The JSON still parsed, so red flags showed as yellow and addressed goals as not addressed"
  - "The family-facing meeting summary was written in the staff member's language, not the family's"
  - "An unknown progress rating fell back to 'regressing', telling parents their child was getting worse"
  - "Converting controllers to ServiceErrorKind changed English statuses that main returned as 400 into 409 or 503"
  - "The 'Generated in English' notice appeared on a transient AI suggestion that is always produced in the viewer's language"
root_cause: logic_error
resolution_type: code_fix
severity: high
tags: [ai, llm, localization, structured-output, enum-normalization, prompt, response-language, audience, http-status, i18n]
---

# Troubleshooting: AI response language — structured-output enums, choosing the audience, and safe fallbacks

## Problem

Phase 3 of `docs/plans/2026-10-06-001-feat-multilingual-english-spanish-plan.md` made AI answers follow the user's language. `ResponseLanguage.SystemLine` adds a fixed Spanish instruction to every prompt except the document parsers. Each artifact records the language it was generated in, and the web shows `GeneratedLanguageNotice` when that differs from the viewer's language.

Three review passes found that "answer in Spanish" interacts badly with prompts that return JSON. Each artifact also needs an explicit choice of *whose* language it uses.

## Environment

- .NET 9, Claude via `ClaudeClient`; JSON-mode prompts whose enum-like fields are stored as plain strings.
- React web code branches on those strings (`severity === 'red'`, alignment status variants).
- Date: 2026-10-06.

## Symptoms and causes

1. **Translated enums parse without error.**
   - The model fields (`Severity`, `Rating`, `AlignmentStatus`, `ProgressRating`, …) are `string`, so `"rojo"` deserializes fine.
   - The UI then quietly mis-bins it: red flags are dropped from urgent counts, and an addressed goal renders as not addressed.
   - This is the dangerous kind of failure, because nothing throws.
2. **Wrong audience.** The family meeting summary used the request culture, which is the *staff* member drafting it.
   - Progress-report analysis first used the owner's language, not the uploader's.
3. **"Conservative" fallbacks that assert facts.**
   - Mapping unknown values to the most severe one is right for severity scales (`red`, `high`).
   - For progress, `regressing` is a factual claim. `concerning` is the cautious value.
4. **Status drift while converting to `ServiceErrorKind`.** Assigning "semantically correct" kinds (Conflict, Unavailable) changed English responses that main returned as 400. The 503 responses would also have started paging Sentry.
5. **A notice on a transient artifact.** The student interview suggestion is never persisted and is always produced in the viewer's language. The client had no field for it, so it defaulted to "en", and the notice was always wrong.

## Solution

- **Spanish instruction:** the language line now says to write only human-readable prose in Spanish. JSON keys, enum/status/severity values, bracketed ids and specified titles/tags stay exactly as given.
- **`AiEnumNormalization`:** one map per field, with Spanish variants mapped to the canonical English values.
  - Each field has an explicit fallback: the most severe value for severity scales, `concerning` for progress.
  - `RemoveNullElements` and null-coalescing of nested objects mean explicit JSON nulls can't crash a run.
- **Audience per artifact:**

  | Artifact | Language source |
  |---|---|
  | Family meeting summary | Family participants (Spanish if any prefers it) |
  | Progress-report analysis | Uploader, then owner, then `en` |
  | Analysis runs, meeting prep | Requester, captured at creation and reapplied in background execution |
  | Advocate | Per message |

  Prompt dates are ISO, so a month name never leaks across languages.
- **Status parity:** `ServiceErrorKind.Validation` is used where main returned 400, and the mapper tests pin the statuses. A status change is a separate, explicit decision.
- **Notices only where they make sense:** `GeneratedLanguageNotice` shows only on persisted artifacts. The transient student suggestion has none.
- **Parsers untouched:** `IepProcessingService` and `EtrProcessingService` never receive the language line, and a test enforces this.

## Verification

- **API:** `dotnet test IepAssistant.Services.Tests`: 1594 passed. This includes `AiEnumNormalizationTests` (Spanish-JSON end-to-end, null elements), family-language tests for the meeting summary, the progress-report language fallbacks, and `ServiceFailureMapperTests` status pins.
- **Web:** `npx vitest run`: 1377 passed. `tsc -b` and `test:types` are clean, and ESLint stays at 36.
- **Bundle:** main chunk 409.72 kB gzip, against a 418 kB budget. The CI size check moves up to Phase 4.
- **Review:** 3 passes (9 P2s → 1 → 0).
- **Not verified:**
  - live Claude responses in Spanish;
  - native-speaker review;
  - null guards for `Sections`, `GoalAnalyses` and `EvaluatedDomains` (todos/249).

## Prevention

- If a prompt returns JSON, a language instruction must say which parts to translate. Normalize every enum-like field on the server, because the UI branches on the exact string.
- Pick each fallback deliberately, per field:
  - **Severity:** use the most severe value.
  - **Assessments:** use the cautious one ("concerning", "insufficient data"), never a negative factual claim.
- For every AI artifact, name its reader before choosing its language: requester, recipient, or family.
- Format dates as ISO in prompts.
- When replacing text-matched status mapping, keep each English status identical to main, and record any intentional status change as its own decision.
- Only show "generated in X" on artifacts that persist and can be seen by someone else.

## Related

- `docs/solutions/logic-errors/2026-10-06-i18n-status-codes-from-localized-text-and-lazy-fallback-language.md`
- `docs/solutions/logic-errors/2026-10-06-i18n-foundation-language-sync-races-shared-devices-and-silent-resx.md`
- `docs/i18n/README.md` (Server-side AI language), `docs/i18n/glossary-es.md`
