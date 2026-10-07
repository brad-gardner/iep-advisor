---
module: "i18n Phase 2 (parent core): API status mapping, web namespace loading"
date: "2026-10-06"
problem_type: logic_error
component: service_object
symptoms:
  - "After server messages were localized, Spanish users got 400 where English users got 403 or 404 on the same request"
  - "Worker's patch matched Spanish word stems ('permiso', 'no encontrad') in translated messages to choose the HTTP status"
  - "English pages briefly showed raw keys such as 'parentPage.welcome', and stayed on them if a locale chunk failed to load"
  - "Typed i18n keys silently stopped checking feature namespaces (Record<string,string> stand-in)"
  - "School-linked children showed 'Specific Learning Disability' and 'Grado: K' untranslated in the Spanish UI"
root_cause: logic_error
resolution_type: code_fix
severity: high
tags: [i18n, localization, http-status, service-result, error-kind, react-i18next, lazy-loading, fallback-language, typed-keys, aspnet-core]
---

# Troubleshooting: status codes derived from localized text, and a lazily loaded fallback language

## Problem

Phase 2 of `docs/plans/2026-10-06-001-feat-multilingual-english-spanish-plan.md` translated the parent pages and their server messages. Two assumptions in the existing code broke as a result. Review pass 1 found 8 P2s; most came from these two causes.

## Environment

- API: .NET 9, `ServiceResult`, about 30 controllers, each with its own `MapFailure`.
- Web: i18next 26.4.2, react-i18next 17.0.16 with `useSuspense: false`, Vite `import.meta.glob`.
- Date: 2026-10-06.

## Symptoms and causes

1. **Status codes chosen from message text.** Controllers chose 403/404/409/503 with `message.Contains("permission")` and `message.Contains("not found")`. Once `ServiceResult.Message` was localized, Spanish requests fell through to 400.
   - The first fix added Spanish stems. That only worked while every translator kept those words, and already failed for natural phrasings such as "No se encontró…".
   - It also missed `EducatorController`, which calls the same service.
2. **English stopped being bundled.** Lazy loading was added for every non-shell namespace in *both* languages. With `useSuspense: false`, react-i18next renders the bare key until a namespace loads. A failed chunk then leaves English pages on keys permanently, and the `failedLoading` → English fallback no longer had a bundled English to fall back to.
3. **Typing turned off.** The lazy namespaces were typed as `Record<string, string>`, so `t('anything')` compiled. The comment claiming the parity test caught typos was false: parity compares the JSON files, not the call sites.
4. **Display helpers didn't normalize.** The school-link code writes server display strings such as "Specific Learning Disability", "K" and "5". The form normalized them; the read-only display helpers did not.

## What Didn't Work

- **Matching translated words to pick a status** (see cause 1).
- **Lazy English plus a `ready` guard.** It would have had to be added to every page, and it still left the failed-chunk case.
- **Refetching on `t` identity changes.** `t` changes identity whenever a namespace finishes loading, so fetches ran twice and refetched on every language switch. Keying the refetch on the saved preference instead lagged behind the language actually in use (still open; see Verification).

## Solution

1. **`ServiceErrorKind`** on `ServiceResult` and `ServiceResult<T>`: None, Validation, NotFound, Forbidden, Conflict, Unavailable.
   - Factories set the kind.
   - The shared `ControllerBase.MapServiceFailure(result)` switches on the kind and keeps the old English substring check only as a fallback for `None`, for services that aren't converted yet.
   - ChildLink and Notification services set kinds, including on propagated sub-results. The ChildLink, Notifications and Educator controllers use the mapper.
   - The Spanish stems were deleted.
   - The plan now requires this before translating any service.
2. **All English is bundled** with `import.meta.glob('/src/locales/en/*.json', { eager: true })`. Only Spanish is lazy.
   - Typing comes from the eager English map, so every namespace is strictly typed again. A `@ts-expect-error` canary covers a feature namespace.
   - The main chunk is 396 kB gzip against the 418 kB budget. The plan schedules role-split English chunks plus a CI size check for the start of Phase 5.
3. **Normalize before lookup in display helpers.** The normalizers moved to `web/src/lib/child-profile-normalize.ts`, which also removed a lib→feature import cycle.
4. **Errors are stored as flags and translated at render time.** `t` was removed from fetch dependencies, and a request counter makes `useChildren` latest-wins.
5. **Carried over from Phase 1 (todos/247):**
   - the pre-login choice is consumed at sign-in;
   - logout and sign-in bump the language generation;
   - a token guard discards a stale `/me` after sign-out.
6. **todos/246 flake.** Two sections' open-focus `requestAnimationFrame` calls competed for focus. The test was reordered, and production now skips the auto-focus if the user has already focused a field.

## Verification

- **API:** `dotnet test IepAssistant.Services.Tests`: 1426 passed. Includes `ServiceFailureMapperTests` and es-culture status tests; a Spanish 400 message containing "permiso" still returns 400.
- **Web:** `npx vitest run`: 1222 passed. `tsc -b` and `test:types` are clean, ESLint stays at 36, and the impeccable detector is clean.
- **Review:** 3 passes (8 P2s → 0 → 2).
- **Open at the cap (todos/248), to be fixed first in Phase 3:**
  - `use-home` refetches on the saved preference, but the server localizes from the language in use (`i18n.resolvedLanguage`).
  - Signing in while a Spanish chunk is still loading can switch to Spanish against an English account, because `syncLanguagePreference` compares against `i18n.language` before the pending switch finishes.
- **Not verified:** real-browser behaviour on a throttled network, and a native-speaker review of the Spanish.

## Prevention

- **Never derive behaviour from user-visible text.** Before localizing messages, grep for `.Contains(` and `==` on `Message`, and give failures a machine-readable kind.
- **Keep the fallback language bundled.** If you split it, load each split with the code that uses it, never at render time.
- **Use the real resource type for i18n key typing.** Add a canary that fails when typing loosens.
- **Normalize stored values in display helpers as well as forms.** A second producer, here server display strings, will appear.
- **Key refetches of server-localized data on the language actually in use** (`resolvedLanguage`), not on the saved preference or `t` identity.

## Related

- `docs/solutions/logic-errors/2026-10-06-i18n-foundation-language-sync-races-shared-devices-and-silent-resx.md` (Phase 1)
- `docs/plans/2026-10-06-001-feat-multilingual-english-spanish-plan.md` ("Decisions added during implementation")
- `docs/i18n/README.md`, `docs/i18n/glossary-es.md`
