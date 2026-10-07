---
module: "i18n Phase 7 (per-language PDFs, last server messages, global lint guard)"
date: "2026-10-07"
problem_type: logic_error
component: service_object
symptoms:
  - "A Spanish copy of a finalized authored document, rendered on demand weeks after finalize, would show the current meeting, participants and team roles instead of the ones the English legal record printed"
  - "After an in-app language switch, the authored PDF Download button kept failing and the IEP PDF button quietly served the cached English file to a Spanish reader"
  - "A poll or retry sent before the switch could resolve afterwards and overwrite the new language's state with the old language's row"
  - "Every PDF query now references the Language column, so deploying the code before the migration would break finalize for IEPs and authored documents"
  - "The literal-message architecture test only checked the first argument of FailureResult, so FailureResult(kind, \"literal\") passed it"
root_cause: logic_error
resolution_type: code_fix
severity: high
tags: [i18n, pdf, legal-record, snapshot, migration, deploy-order, polling, race-condition, language-switch, architecture-test]
---

# Troubleshooting: per-language PDFs — frozen headers, deploy order, language-switch races

## Problem

Phase 7 of `docs/plans/2026-10-06-001-feat-multilingual-english-spanish-plan.md` renders finalized IEP and authored-document PDFs in the reader's language.

- **One row per language.** Each version keeps one PDF row per language: `(versionId, Language)`, with filtered unique indexes.
  - English keeps its existing blob paths, and legacy rows with a NULL language count as English.
  - Spanish uses `…​.es.pdf`.
- **Spanish is rendered on demand.** The first status poll from a Spanish reader creates a Pending row. The controller queues the render after the commit.

Lazy rendering introduced new failure modes, which three review passes found.

## Environment

- .NET 9 / EF Core with SQL Server (QA) and SQLite (tests). QuestPDF.
- React 19 / react-i18next 17. The language switches in-app without a reload, and `api-client` sends `Accept-Language` from the `@/lib/i18n` singleton.
- Date: 2026-10-07.

## Symptoms and causes

1. **Live data inside a legal record.**
   - `AuthoredDocumentPdfService.BuildHeaderContextAsync` reads current tables at render time: student facts, the *latest* Held meeting, participants, and owner roles.
   - That was harmless when rendering happened only right after finalize. A Spanish render triggered months later can print different facts from the English copy of the same version.
   - The IEP version PDF reads only its frozen aggregate, so it was not affected.
2. **The client cached one language's status.** Both PDF hooks fetched once per `versionId`. After a switch, the cached English `Rendered` status and url stayed in place.
   - The server resolves the row from `Accept-Language`, so the Spanish row was never created.
   - The authored download was refused, and the IEP button served the English url.
3. **Stale in-flight answers.** Resetting on a language change is not enough. A poll or retry sent under English can resolve after the Spanish fetch and overwrite it.
   - The status response has no language field, so the guard has to live on the client.
   - The guard must read the same i18n instance as `api-client`. In tests, the instance from `useTranslation()` did not reflect the singleton's `language`.
4. **Deploy order.** The QA deploy ships code only, and migrations are applied by hand. New code that writes and filters on `Language` fails against the old schema.
5. **The architecture test had a narrow scanner.** It only flagged a literal right after `FailureResult(`. Once services moved to `FailureResult(ServiceErrorKind.X, …)`, that position always held the kind.

## Solution

- **Frozen header.** `AuthoredDocumentPdf.HeaderSnapshotJson` is stored on the English row (migration `AddPdfHeaderSnapshot`).
  - `ResolveHeaderContextAsync` reuses the snapshot when one exists.
  - Otherwise it builds the header live and stores it, even if the render then fails, so the first resolution wins for every language and every retry.
  - An English first render is byte-identical to before.
  - An unreadable snapshot is logged, and the header is built live, so a stored snapshot can never wedge every retry in Error.
- **Language-aware hooks.** `use-pdf-status.ts` and `use-authored-pdf-status.ts`:
  - track the active language and reset status and url during render when it changes;
  - re-fetch on that change, which creates and queues the new language's row;
  - ignore the English-row `initialStatus` seed while a non-English language is active;
  - in `fetchStatus` and `retry`, capture `appI18n.language` before the request and drop the answer if it changed while in flight.
- **Race-safe first poll.** `GetPdfStatusAsync` catches the unique-index `DbUpdateException`, detaches the loser's entity, re-reads the winner's row, and returns it with `NeedsRender=false`. Other causes find no matching row and are rethrown.
- **Rollback.** The `AddPdfLanguage` Down deletes non-English rows before restoring the single-column unique indexes. The after-commit enqueue uses `CancellationToken.None`.
- **Guard.** `NoLiteralFailureResultMessagesTests` flags a literal or `$"…"` in *any* top-level argument of `FailureResult`, `NotFound`, `Forbidden`, `Conflict` and `PaymentRequired`. Its string skipping understands verbatim strings. It immediately found one interpolated English message, in `EducatorService` bulk assign, which is now localized.
- **Operations.** Apply the migrations *before* deploying code. They are additive and safe while the old code runs, because old code writes NULL-language rows and the filter exempts those. To roll back, run Down first, then redeploy the old code.

## Verification

- **API:** `dotnet test IepAssistant.Services.Tests`: 1808 passed. This includes:
  - the Spanish-after-English snapshot reuse when the latest meeting changes;
  - retry reusing the snapshot;
  - the fallback for a corrupt snapshot;
  - the race re-query mechanism;
  - per-language rows and blob paths.
- **Web:** `npx vitest run`: 1593 passed. This includes hook tests for:
  - a language switch triggering a re-fetch and clearing the url;
  - the seed being ignored for a non-English language (asserted `null`);
  - a deferred English retry that resolves after the switch being dropped.

  `tsc -b` is clean and ESLint stays at the 35 baseline.
- **Migrations:** `AddPdfLanguage` and `AddPdfHeaderSnapshot` were applied to QA SQL Server before the merge. `ef migrations list` shows neither as pending.
- **Review:** 3 passes. P2 count went 3 → 1 → 0, with no P1 at any point. Pass 3 left one P3: the guard test exists only in the IEP hook.
- **Not verified:**
  - a real browser language switch on a PDF page;
  - multi-instance render races. The worker is serial within one process, and the last write wins across instances;
  - native-speaker review of the Spanish.
- **Known limitations (todos/249):**
  - A legacy version that was finalized and rendered in English before this deploy has no snapshot. Its first Spanish render freezes the facts current at that time.
  - Participant and owner role names and the "Unknown" fallback are still English inside the Spanish PDF.
  - The production race catch is exercised only indirectly.

## Prevention

- Anything rendered *later* from a finalized record must read frozen inputs. If it needs context that isn't frozen, snapshot that context at the first render and reuse it.
- When server responses depend on a client setting that can change without a reload (language, tenant, role), the client cache must be keyed on that setting. Every async write must check that the setting hasn't changed since the request was sent.
- Compare against the same instance the transport reads from, not a hook's copy.
- A migration that new code depends on, in an environment where deploys don't migrate, ships with "apply before deploy" and "Down before rollback" written into the PR.
- Architecture tests that grep call shapes need re-checking whenever the call shape changes.

## Related

- `docs/solutions/logic-errors/2026-10-07-i18n-admin-surfaces-typed-input-tokens-and-shared-load-errors.md`
- `docs/solutions/logic-errors/2026-10-07-i18n-role-split-locales-and-lazy-chunk-boundaries.md`
- `docs/solutions/logic-errors/2026-10-06-i18n-status-codes-from-localized-text-and-lazy-fallback-language.md`
- `docs/i18n/README.md`
