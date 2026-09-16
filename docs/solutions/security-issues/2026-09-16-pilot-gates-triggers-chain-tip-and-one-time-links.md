---
module: "PilotGates"
date: "2026-09-16"
problem_type: "security_issue"
component: "background_job"
symptoms:
  - "Every audit batch insert failed on QA with 'target table cannot have any enabled triggers … OUTPUT clause' and the daily integrity check passed over 0 rows"
  - "The audit hash chain kept its tip in memory, so two API instances (a rolling deploy) would fork the chain and page admins as if the log had been tampered"
  - "Magic-link and cancel-deletion tokens travelled in Sentry breadcrumbs and sat in plaintext in the outbound email table forever"
  - "Under React StrictMode the sign-in-link page consumed its single-use token and then hung on the spinner"
  - "A parent purge deleted the local user while Stripe kept the customer and the subscription"
root_cause: "missing_validation"
dotnet_version: "9.0"
resolution_type: "code_fix"
severity: "high"
tags: [audit-log, hash-chain, sql-server-triggers, ef-core, output-clause, serializable, outbound-email, redaction, sentry, breadcrumbs, strict-mode, stripe, account-purge, seed-demo, magic-link, ci, code-review]
---

# Pilot gates: declare your triggers, keep the chain tip in the database, and treat every one-time link as a secret at rest

## Problem
Plan 8 (`docs/plans/2026-09-15-008-…`) closed the operational gaps a real-data pilot would hit: a durable,
tamper-evident audit log guarded by SQL Server triggers; queued, retried, visible outbound email; a
30-day account purge with a signed cancel link; PII-free telemetry; a seeded demo district; CI that
gates deploys; magic-link sign-in; and a public trust page. Two backend workers and a web worker built
it; gates were green. The first live check and four review passes found six P1s and twenty-six P2s
— almost all of them in the "guarantee" features themselves.

## What went wrong

1. **EF Core and triggers.** EF Core 7+ saves with an `OUTPUT` clause unless the model declares the
   table has triggers. The four new `INSTEAD OF UPDATE/DELETE` triggers therefore made SQL Server reject
   every audit batch insert (each retry, then staging — forever), and the hash backfill. The daily
   integrity check reported *Ok* because it walked zero hashed rows. The unit suite runs on SQLite and
   could not see it.
2. **A chain tip in memory.** The worker cached `_lastHash` and called itself "the only writer".
   True per process, false per deployment: two instances would each extend from their own tip.
3. **Secrets in places nobody looked.** Sentry's navigation breadcrumbs record the literal
   `pushState` argument — `/auth/magic?token=…` — and `beforeSend` only scrubbed `request`/`user`.
   The outbound email queue kept full HTML bodies (sign-in links, reset tokens, RSVP tokens) after
   sending, with no retention.
4. **"Run exactly once" done with a per-invocation flag.** StrictMode's mount→cleanup→mount left the
   real request's `active` closure false; the token was consumed and the page never learned.
5. **Purge that forgot the billing system, the recovery codes, and the blob that failed.** And a CI
   job that depended on a git-ignored settings file, so it would have blocked every deploy.

## Solution

- `ToTable(t => t.HasTrigger("TR_…_Immutable"))` on the four guarded entities — verified live: 94 rows
  backfilled, 3 stranded events replayed, a DBA-style tamper → `Broken` + admin notification.
- Chain tip read inside each batch's SERIALIZABLE transaction (`AccessAuditLogWorker.InsertRowsAsync`);
  no in-memory tip; keyset backfill with a filtered `[Hash] IS NULL` index; bounded 20k channel with a
  visible drop counter; invariant-culture hash input; a two-writer test.
- `scrubSentryBreadcrumb` (`beforeBreadcrumb`) + `request.url` scrub; the consume/cancel pages
  `history.replaceState` the token away before the POST. `OutboundEmailKinds.CarriesOneTimeSecret`
  bodies are redacted at terminal state and cannot be re-sent (includes meeting RSVP emails).
- A `mountedRef` re-armed by every effect invocation, with the run-once guard kept separate.
- `IStripeAccountCleanup` (cancel subscription, delete customer) before the local row goes; blob
  failures other than 404 abort the purge for retry; recovery codes/reset/magic tokens removed on
  purge and anonymise; cancel link expires with the 30-day grace independent of the worker; stale
  `Sending` rows reclaimed; CI supplies startup settings as `env:` placeholders (proved on a clean
  worktree).
- Also from review: login toggle focus + `finally` loading reset, live regions on outcome pages,
  status-driven polling on the email admin page, magic-link refusal as 400 (the client treats 401 as
  session death), demo domain `mapleridge.example`, trust page claims reworded to what is actually
  guaranteed and by whom.

## Why this addresses the root cause
Each guarantee had been implemented at the layer that *made* it and verified at the layer that could
not *see* it: triggers verified by SQLite tests, a chain verified in one process, PII scrubbing
verified on the event but not the breadcrumbs, run-once verified without StrictMode. The fixes move
the check to where the guarantee actually has to hold (the DB transaction, the browser's history
API, the queue row's terminal state, a clean CI checkout) and add the test that runs there.

## Verification
- `dotnet build IepAssistant.sln && dotnet test IepAssistant.Services.Tests` → **884 passed**; `dotnet ef
  migrations has-pending-model-changes` clean (also on a clean worktree with env-only config).
- `npx tsc -b --noEmit && npm run test:types && npx vitest run` → **497 passed**; lint at the 36-error
  baseline; `npm run build`; `npm run guard:ux`; workflow YAML parsed.
- Live QA: audit backfill/replay/integrity/tamper; email queue drains; `seed-demo` (65 s) / `--reset`
  (54 s) / re-seed; demo login + 40-student roster; magic link 202 / single-use / MFA refusal / 400.
- Review: 8 configured reviewers (Sonnet). Pass 1 → 5 P1 / 17 P2 / 6 P3; pass 2 → 1 P1 / 8 P2;
  pass 3 → 1 P2 (the two-writer test was not actually concurrent under SQLite's synchronous async);
  pass 4 (targeted) → recorded in `todos/REVIEWED`.
- Limitations: SQL Server's SERIALIZABLE range-lock behaviour between two live instances is argued and
  tested against SQLite's writer lock (two threads behind a barrier), not exercised on SQL Server; the
  seed timing was measured over the public internet.

## Prevention
- Any table that gets a trigger gets `HasTrigger` in the same migration — and a live smoke of one
  INSERT on SQL Server, because SQLite will not tell you.
- Anything that must be unique across instances (a chain tip, a claim) lives in the database inside
  the transaction that uses it, never in a process field.
- Every URL that can carry a one-time token is scrubbed in three places: the address bar as soon as it
  is read, Sentry breadcrumbs/`request.url`, and the stored email body once it is sent.
- "Run once" effects pair a run-once ref with a mounted ref that every invocation re-arms.
- A CI job that boots the app gets its configuration from the workflow, not from a file the
  repository ignores; prove it on a clean worktree before it gates deploys.

## Related
- Plan: `docs/plans/2026-09-15-008-chore-pilot-gates-durable-audit-seed-demo-ci-hygiene-plan.md`
- Findings: `todos/135–166` (P3 follow-ups: `todos/156-pending-p3-pilot-gates.md`)
- Prior learnings: `docs/solutions/security-issues/2026-09-16-record-lifecycle-…`, `docs/solutions/logic-errors/2026-09-16-family-draft-sharing-…`
