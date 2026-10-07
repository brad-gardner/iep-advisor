---
module: "i18n Phase 6 (district + platform admin, roster/staff import)"
date: "2026-10-07"
problem_type: logic_error
component: frontend_react
symptoms:
  - "Spanish roster-import error told admins to enter 'Sin grado', a value the parser rejects, so following the hint produced the same error again"
  - "Staff import (half of the same import wizard) returned English errors with no ServiceErrorKind while its controller had been converted"
  - "One phase introduced three different shapes for 'load error, translate at render' across hooks"
  - "The shared step indicator still built an English accessible name ('Step 1 of 5: Plantilla')"
  - "Converted admin pages still formatted dates with the browser locale instead of the app language"
root_cause: logic_error
resolution_type: code_fix
severity: medium
tags: [i18n, import, csv, typed-input, accessibility, load-error, date-format, admin, spanish]
---

# Troubleshooting: admin surfaces — typed input tokens, shared load errors, leftover English

## Problem

Phase 6 of `docs/plans/2026-10-06-001-feat-multilingual-english-spanish-plan.md` covered:
- **Web:** district admin, staff invites, roster import, exports and platform admin.
- **API:** their server messages, plus the last controllers that still mapped status from message text.

Review found problems that came from translating text users are meant to type, and from parallel workers each inventing their own pattern.

## Environment

- React 19 / react-i18next 17, staff namespaces loaded with their lazy chunks.
- .NET 9, `ServiceErrorKind` and `MapServiceFailure`.
- Date: 2026-10-07.

## Symptoms and causes

1. **Translated input tokens.** Import row errors list the values the parser accepts. "Ungraded" was translated to "Sin grado", which the parser rejects.
   - CSV header names and enum values in import files are a file format, not UI copy.
2. **Half-converted feature.** `DistrictImportsController` serves both roster and staff import. Only `RosterImportService` was converted.
   - The controller's doc comment claimed staff import had been converted too.
3. **Pattern drift across parallel workers.** Three shapes appeared for the same idea: a `''` sentinel string, a boolean, and a `{kind}` union (re-declared locally about 45 times across the codebase).
4. **Shared components missed by feature-scoped work.** `ProgressDots` lives in `components/ui`, outside every feature folder, so no phase converted it. Feature pages used `toLocaleString()` with no locale argument.

## Solution

- **Import tokens:** typed input tokens stay literal in every language (`Ungraded`, `Active/Exited/Archived`, role names, column headers). Only the explanation around them is translated.
- **Staff import:** `StaffImportService` converted the same way as roster import. Every failure has a kind, row messages are localized, and English statuses match main. Real `ErrorKind` assertions now cover the 7 services converted in Phase 6, each on an actual failing call.
- **Load errors:** `lib/api-error.ts` now has `LoadError`, `toLoadError(resOrErr)` and `loadErrorText(e, fallback)`. This phase's hooks use them, and `docs/i18n/README.md` names this as the pattern going forward. Older copies migrate later (todos/249).
- **Step indicator:** `ProgressDots` uses one interpolated common key with a context variant. Its Spanish accessible name is now `Paso 1 de 5: Plantilla`.
- **Dates:** admin dates and numbers use `formatDate` or `getActiveLanguage()`.
- **Audit log:** each row renders as one `<Trans>` sentence per action, with escaped user names.
- **API sweep:** no controller in the API still maps status from message text. The English-substring fallback inside `MapServiceFailure` now only covers services that haven't been converted yet.

## Verification

- **Web:** `npx vitest run`: 1582 passed. `tsc -b` is clean. ESLint dropped to 35 errors, and the CI baseline was lowered to 35. The main chunk is 307.69 kB gzip.
- **API:** `dotnet test`: 1776 passed.
- **Review:** 2 passes. Pass 1 found 5 P2s; pass 2 found none.
- **Not verified:** native-speaker review of the Spanish.

## Prevention

- Before translating a message, check whether any part of it is something the user types into a file or form. Keep those tokens literal.
- When a controller serves two services, convert both or neither, and keep the doc comment accurate.
- Before running parallel conversions, put the shared pattern in a shared module, and point the workers at it.
- Each phase should search `components/ui` and other shared code for strings its pages render, not only its own feature folders.

## Related

- `docs/solutions/logic-errors/2026-10-07-i18n-role-split-locales-and-lazy-chunk-boundaries.md`
- `docs/solutions/logic-errors/2026-10-06-i18n-status-codes-from-localized-text-and-lazy-fallback-language.md`
- `docs/i18n/README.md`, `docs/i18n/glossary-es.md`
