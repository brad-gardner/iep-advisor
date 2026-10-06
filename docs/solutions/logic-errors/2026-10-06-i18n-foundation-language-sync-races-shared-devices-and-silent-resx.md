---
module: "i18n foundation (web AuthProvider language sync, API request localization)"
date: "2026-10-06"
problem_type: logic_error
component: frontend_react
symptoms:
  - "Switching languages quickly, or while /me was in flight, could leave the screen in one language and the saved account preference in the other"
  - "On a shared device, the previous user's language was saved to the next user's account when that account had no preference yet"
  - "IStringLocalizer returned the raw key for every Spanish string, with no exception, because the resx manifest names were wrong"
  - "Turning on RequestLocalization for Spanish users also changed CurrentCulture, so English emails and notifications went out with Spanish date formats"
  - "The staff invite sentence came out as '…le invitó a unirse como Teacher' because the role label and sentence fragments weren't translated as one unit"
root_cause: async_timing
resolution_type: code_fix
severity: high
tags: [i18n, react-i18next, aspnet-core, request-localization, resx, race-condition, shared-device, latest-wins, auth]
---

# Troubleshooting: i18n foundation — language-sync races, shared-device leaks, silent resx misses

## Problem

Phase 1 of `docs/plans/2026-10-06-001-feat-multilingual-english-spanish-plan.md` added the multilingual foundation:
- react-i18next with typed keys, English bundled, Spanish lazy-loaded;
- `User.PreferredLanguage`;
- ASP.NET Core `RequestLocalization`;
- `.resx` + `IStringLocalizer`.

The site picks a language in this order: account preference, then pre-login choice, then browser, then `en`. The first sign-in with no saved preference writes the resolved language back to the account.

Three review passes found that "persist the language on the account" has more race and ownership edges than it seems to.

## Environment

- Web: React 19, i18next 26.4.2, react-i18next 17.0.16, Vitest/jsdom.
- API: .NET 9, ASP.NET Core localization, EF Core 9.
- Date: 2026-10-06.

## Symptoms and causes

1. **A quick switch saves the wrong language.**
   - Spanish loads lazily and English is bundled, so `changeLanguage('es')` resolves *after* a later `changeLanguage('en')`.
   - In i18next 26, a superseded `changeLanguage` promise still resolves; it only skips applying the language.
   - Code that awaits it and then PUTs therefore saves the stale choice.
2. **A response that was already in flight overwrote an explicit switch.** The `/me` sync applied the older account value, or a null-preference backfill PUT landed after the user's explicit PUT.
3. **Shared-device leaks.** Twice:
   - Logout copied the active language into the pre-login key, and the next user's null-preference backfill saved it.
   - After that was fixed, the *explicit* pre-login key itself was never cleared, which is still open; see Verification.
4. **Silent resx miss.** The .NET SDK drops a folder literally named `Resources` from the manifest resource name. `AddLocalization(ResourcesPath="Resources")` then finds nothing, and `IStringLocalizer` returns the key with `ResourceNotFound=true`. There is no exception.
5. **Culture leak.** Setting `SupportedCultures` (not just `SupportedUICultures`) to `[en, es]` changed `CurrentCulture`. English text built with `{date:MMMM d}` then picked up Spanish month names, for recipients who prefer English.
6. **Mixed-language sentences.** Each sentence fragment was translated separately, while the role label came from an English-only map.

## What Didn't Work

- **Awaiting `changeLanguage` and then PUTting.** It assumes the promise means "this language is now active", which isn't true when a later call supersedes it.
- **Writing the active language to the pre-login key at logout** "so the login page stays in that language". It made a display hint into a value that later gets persisted.
- **A `prev.preferredLanguage` guard in the merge.** It duplicated the generation guard and its comment named the wrong race.
- **Hand-copying the RequestLocalization options into tests.** The tests passed against a copy, not the real config.

## Solution

**Web (`web/src/features/auth/stores/auth-context.tsx`, `web/src/lib/i18n/detect.ts`):**
- **Latest-wins `languageGenerationRef`:**
  - `setLanguage` bumps it before awaiting.
  - Each await boundary re-checks it.
  - `/me` sync and backfill capture it before their fetch and drop their work if it has been superseded.
- **Ordering:** `setLanguage` awaits the pending backfill PUT (`pendingBackfillRef`) before sending its own, so the explicit choice lands last on the server.
- **Load failures:** after `changeLanguage`, if `i18n.resolvedLanguage !== language`, return an error and don't persist.
- **Two separate signals:**
  - an *explicit* pre-login choice (switcher or `?lang=` while signed out), which may seed a new account;
  - a *display-only* sign-out carry-over, which is never persisted and is cleared on sign-in.
  
  The backfill uses `explicit ?? browser`, never `i18n.language`.
- **Startup:** `detectInitialLanguage()` owns startup order (cached account → explicit → carry-over → browser → en), passed as `lng` to init. The language-detector plugin was removed.
- **Sentences:** each is one interpolated key rendered through `<Trans>` with `escapeValue: true` + `shouldUnescape` for user text. Role labels are translated keys. `context` is avoided as a variable name because i18next reserves it.

**API:**
- `RequestLocalizationSetup.Configure` is shared by `Program.cs` and the tests. `SupportedCultures=[en]` and `SupportedUICultures=[en, es]`, so only UI strings change. `CultureScope` sets the UI culture only.
- Wildcard `<EmbeddedResource Update="Resources\*.resx" LogicalName="$(RootNamespace).Resources.%(Filename).resources" />` with a comment explaining why it's needed.
- `OnTokenValidated` stashes the user's `PreferredLanguage` in `HttpContext.Items`, so the culture provider makes no second user query.
- `SupportedLanguages.ForRecipient(saved)` falls back to the request's UI culture for account emails.

**Guard rails for later phases:**
- the en↔es key/placeholder/tag parity test;
- a `missingKeyHandler` that fails tests (including `<Trans>` keys);
- a `@ts-expect-error` canary so key typing can't silently degrade to `any` (`skipLibCheck` hides broken `.d.ts` imports);
- per-folder `no-literal-string` lint.

## Verification

- **API:** `dotnet test IepAssistant.Services.Tests`: 1397 passed.
- **Web:** `npx vitest run`: 1093 passed. `tsc -b` and `test:types` are clean, ESLint stays at the 36 baseline, and the impeccable detector is clean.
- **English bundle:** +26 kB gzip (+7.2%) against a 15% budget. Spanish chunks are lazy.
- **Review:** 3 passes (5 P2s → 1 → 2).
- **Open at the review cap (todos/247), to be fixed first in Phase 2:**
  - The explicit pre-login key isn't cleared after sign-in, so a later null-preference account on the same device inherits it.
  - Logout doesn't bump the generation, so an in-flight language PUT can restore the user after sign-out.
- **Not verified:** a real browser on a throttled network, and native-speaker review of the Spanish.

## Prevention

- Any "persist after async apply" flow needs a latest-wins token checked after every await. Never treat a library's resolved promise as proof that the change took effect; check the resulting state (`resolvedLanguage`).
- Everything the client stores locally on a device has an owner. Ask "could the *next* person on this device inherit this?" before persisting a value derived from local state. Clear a per-visitor choice once it has been used.
- Every sign-out must invalidate in-flight per-user async work (bump the generation or abort).
- With ASP.NET Core localization, localize the **UI culture only** unless you mean to change number and date formatting for everything rendered in that request.
- After adding resx files, assert a non-English string resolves in a test. `IStringLocalizer` fails silently.
- Translate whole sentences, never fragments. Route display-label maps through the translation layer.

## Related

- `docs/plans/2026-10-06-001-feat-multilingual-english-spanish-plan.md`
- `docs/designs/2026-10-06-multilingual-english-spanish-design.md`
- `docs/i18n/README.md`, `docs/i18n/glossary-es.md`
- `docs/solutions/logic-errors/2026-10-06-free-text-to-dropdown-legacy-values-and-not-set-clear.md` (stored values vs. display labels)
