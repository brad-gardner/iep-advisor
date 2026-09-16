---
title: "chore: Pilot gates — durable audit, delivery visibility, account closure, demo seed/reset, telemetry review, trust page & school marketing track, review debt, docs refresh"
type: chore
status: completed
date: 2026-09-15
origin: docs/gap/combined-findings.md
slicing_approach: vertical
series: school-sale-readiness 8/8
depends_on: docs/plans/2026-09-15-007-feat-record-lifecycle-signatures-amendments-goal-history-export-plan.md
---

# chore: Pilot gates and hygiene (combined findings §6, hygiene list, C11-adoption slice)

## Problem Statement

Section 6 of the combined findings lists evidence/operational gaps that block a real-data pilot even once features exist: fire-and-forget audit writes can drop events; email failures can no-op silently; account deletion has no purge worker and conflicts with revocation; Sentry defaults may send PII; there is no synthetic demo environment; five P2 review todos are open; root docs are stale; the marketing site has no school track; there is no trust/accessibility page.

## Decisions (defaults)

1. **Durable audit:** `AuditLogger.Record` writes to a bounded channel **and** the worker persists in batches with retry; on worker failure or shutdown, undrained entries are flushed to a `PendingAuditEvent` table via the hosted service's `StopAsync`; a startup pass replays them. Add `AuditLog` integrity: `CreatedAt`/`Id` ordering index already exists; add a nightly `AuditIntegrityCheck` counting rows vs. a running hash chain (`PrevHash`, `Hash` columns) so tampering is detectable. Raw-SQL immutability: add SQL Server triggers `INSTEAD OF UPDATE/DELETE` on `AccessAuditLogs`, `AuthoredDocumentVersions`, `IepVersions`, `SharedDraftRevisions` (migration with raw SQL; SQLite tests skip).
2. **Delivery visibility:** `EmailService` no longer swallows — it throws `EmailDeliveryException`; all callers go through `IOutboundEmailQueue` (new `OutboundEmail` table: to, subject, body, status, attempts, lastError) drained by `OutboundEmailWorker` with retry/backoff; platform admin page `/admin/email` lists failures with resend; startup logs a WARN banner when ACS is unconfigured in non-Development.
3. **Account closure:** `AccountPurgeWorker` executes deletions 30 days after `DeletionRequestedAt` (parent accounts: children, documents, blobs, analyses; staff accounts: deactivate profile + anonymise user, never delete school records); cancellation endpoint made reachable for a deactivated-token user via a signed link emailed at request time; tests for both paths.
4. **Telemetry:** Sentry `sendDefaultPii=false`, scrub user email (keep numeric id), `beforeSend` drops request bodies; API Serilog enrichers reviewed — no document text logged (grep + test on the assist/analysis paths).
5. **Demo seed/reset:** `dotnet run --project IepAssistant.Api -- seed-demo [--reset]` (command-line switch handled before host start) creating **fictional** "Maple Ridge Local Schools": 3 schools, 9 staff across all roles, 40 students with external IDs/enum values/timeline dates spread over ±90 days, teams, 15 finalized OH IEPs + 5 ETRs with goals/observations, 6 meetings in the next 30 days, 2 linked parents (one with a shared draft + responses), 1 student account, 1 evaluation case, contact attempts; all accounts use `Demo!2026pw`. `--reset` deletes only rows tagged by the seed's district. Guarded to non-Production.
6. **CI:** in addition to plan 1's test gates, add e2e smoke job (Playwright against a SQLite-backed API? — no: keep e2e out of CI for now, document as manual pre-release step) and a `migrations-check` job (`dotnet ef migrations has-pending-model-changes`).
7. **Review debt:** resolve `todos/P2-01..05` (catch ordering, invalid-response refund, refund ordering, SDK exception classification, duplicated classification) and `P3-01`; delete the todo files when done.
8. **Docs & marketing:** rewrite root `README.md` (stack, ports 7200/5200, seed-demo, test commands, links to wiki/plans); archive `PLAN.md`/`PROGRESS.md` under `docs/archive/`; marketing `index.html` gains a "For schools & districts" section (value, supported scope: Ohio IEP/ETR, demo/pilot CTA mailto) and a `/trust.html` page (data handling, retention/deletion, sub-processors, AI governance: human approval + citations + no training on customer data *stated only as configured*, accessibility statement WCAG 2.1 AA target, DPA available on request); in-app `/trust` link in the sidebar footer. Replace the Google Form support link with `mailto:support@…` + response expectation.
9. **Adoption slice (C11):** magic-link sign-in for staff invited as `RelatedServiceProvider`/`GeneralEducator` (email a 15-minute link; sets session; MFA-exempt only if district policy allows — default requires MFA on first full login), and provider notification deep-links land on the exact "Log progress"/"Provide input" form (already in plan 7) — verify.

## Implementation Phases

### Phase 1 — Durable audit + immutability triggers + delivery queue
- Pending-event table, batch worker with retry, shutdown flush/replay, hash chain + integrity job, triggers migration; outbound email queue/worker/admin page/startup banner; migrate all `EmailService` callers.
- Tests: worker crash mid-batch → replay; hash chain detects tamper; failed send recorded and retried; unconfigured ACS warns.

### Phase 2 — Account closure + telemetry + review debt
- Purge worker, signed cancel link, staff anonymisation; Sentry config; log-content tests; resolve P2-01..05 + P3-01 with tests.
- Tests: purge removes blobs/rows; staff purge keeps documents; cancel after deactivation; classification tests from todos.

### Phase 3 — Demo seed/reset + CI migrations check + magic link
- Seeder command, reset, guards; CI job; magic-link flow + email + tests.
- Checkpoint: fresh QA reset + `seed-demo` yields a demo-able district in < 60 s.

### Phase 4 — Docs, marketing, trust
- README rewrite, archive stale docs, marketing school section + trust page, sidebar trust link, support link; update `docs/ops/2026-07-01-pilot-golden-path-runbook.md` for the new flow (seed-demo, ports, import, meetings).

## Acceptance Criteria

- [x] Audit events survive worker failure/restart; a modified audit row is detected by the integrity job; UPDATE/DELETE on audit and version tables is rejected at the database.
  - Live on QA: startup hashed 94 historical rows and replayed 3 pending events stranded by an earlier failure; `UPDATE`/`DELETE` on `AccessAuditLogs`/`AuthoredDocumentVersions` rejected by `TR_*_Immutable`; a DBA-style tamper (trigger disabled) → integrity run `Broken`, `firstBrokenId 2`, `AuditIntegrityBroken` notification + email to platform admins; restored → `Ok` over 98 rows.
- [x] Every outbound email is queued, retried and its failure visible/resendable by a platform admin; non-dev start without ACS logs a warning.
  - Live: password reset + three integrity alerts queued (`OutboundEmails`) and drained by `OutboundEmailWorker` (Development "would be sent" path → Sent); `/api/admin/email` lists/resends/cancels; `/api/admin/email/status` reports configured/queued/failed; ACS-unconfigured warning outside Development (AcsEmailTransportTests).
- [x] Deletion requests are purged after 30 days with blob cleanup; cancellation works for a revoked session; staff purge preserves school records.
  - AccountPurgeServiceTests (parent purge removes rows + blobs, staff purge anonymises and keeps versions); AccountServiceTests: signed cancel token works after the session was revoked, rejects expired/forged; anonymous `POST /api/account/cancel-deletion`.
- [x] Sentry sends no email/PII; no document text in logs (test asserts).
  - web `sendDefaultPii: false` + `scrubSentryEvent` (request data, user email/username/ip, auth headers) with a unit test; API `LogSafety` + a Serilog capturing-sink test asserting no draft text or parent question is logged by DocumentAssistService/DraftQuestionService.
- [x] `seed-demo`/`--reset` produce and remove the fictional district; blocked in Production.
  - Live on QA: `seed-demo` built Maple Ridge Local Schools (3 schools, 9 staff, 40 students, 20 finalized docs with rendered PDFs, 30 goal records / 88 observations, 6 meetings, 2 parents, shared revision + responses, evaluation case) in 65 s from a laptop over the public internet (58 s reset); idempotent second run 0.9 s; refused in Production; demo login + 40-student roster verified.
- [x] CI: backend + web tests + pending-migration check gate deploy.
  - `.github/workflows/checks.yml` (backend-tests, web-checks with a 36-error lint baseline gate, migrations-check via `dotnet ef migrations has-pending-model-changes` under `ASPNETCORE_ENVIRONMENT=CI`) runs on PR/push and is `uses:`d by `deploy-api.yml`/`deploy-web.yml` before build/deploy.
- [x] `todos/P2-*` and `P3-01` resolved and removed.
  - P2-01..05 and the correctness items of P3-01 implemented with tests (`AnalysisRunService`, `ClaudeClient`, `AnthropicOptions`); the six todo files deleted (P3-01 #6/#8 test-only cleanups skipped).
- [x] README current; PLAN/PROGRESS archived; marketing has a school track and trust page; runbook updated; support link replaced.
  - README rewritten (stack, ports 7200/5200, seed-demo, checks, conventions); `PLAN.md`/`PROGRESS.md` → `docs/archive/`; marketing school track + `trust.html` (sitemap entry, nav/footer links); runbook covers seed-demo/import/authoring/sharing/meetings/records; sidebar support link → `mailto:support@iep-advisor.com` + "Trust & privacy" footer link.
- [x] Magic-link sign-in works for provider/gen-ed roles.
  - Live on QA with the demo gen-ed teacher: request → 202 (unknown email also 202), queued email carries a single-use 15-minute token; consume → the district's `RequireMfaForMagicLink` default returns the MFA-setup refusal; reuse and a forged token → 400. Web `/auth/magic` page + "Email me a sign-in link" on the login page.
- [x] All checks pass.
  - `dotnet build` + `dotnet test` (876 passed); web `tsc -b`, `test:types`, vitest (493 passed), lint (36-error baseline unchanged), `build`, `guard:ux`.

## Dependencies & Risks

- Triggers are SQL Server-specific; tests on SQLite skip via provider check.
- Seed depends on all prior plans' entities.

## Sources

- Origin: [combined-findings](../gap/combined-findings.md) §6, "Release and demo hygiene", C11 adoption; `todos/P2-0[1-5]`, `todos/P3-01`
- Code: `AuditLogger.cs`, `AccessAuditLogWorker.cs`, `EmailService.cs`, `AccountService.cs`, `web/src/lib/sentry*`, `Program.cs`, `.github/workflows/*.yml`, `marketing/index.html`

## Implementation Notes (2026-09-16)

- Branch `chore/pilot-gates`; migrations `AddPilotGatesPhase12` (audit hash chain + pending events + integrity runs + outbound emails + four SQL Server triggers; five FKs onto `SharedDraftRevisions` changed Cascade→Restrict because SQL Server forbids INSTEAD OF triggers on cascade targets) and `AddPilotGatesPhase3` (District.IsDemo/MagicLinkEnabled/RequireMfaForMagicLink, MagicLinkTokens) applied to QA.
- Coordinator fix during live checks: the four trigger-guarded entity configurations declare `HasTrigger` — without it EF Core used an OUTPUT clause and SQL Server rejected every audit batch insert and the hash backfill (the integrity check trivially passed over 0 rows). Also: magic-link consume answers a refused token with 400 (not 401, which the web client treats as session death).
- Deviations: trigger carve-outs (`Hash/PrevHash` one-time set on audit rows; `SignatureStatus` on versions; `Status/WithdrawnAt/WithdrawnByUserId` on shared revisions); `DocumentInstanceService.DeleteAsync` now refuses to delete a draft that has been shared (FK Restrict) instead of cascading family history away; magic-link MFA-setup returns a labelled refusal (`requiresMfa + mfaSetupRequired`) because no enrolment-forcing flow exists; the lint-baseline gate lives inline in `checks.yml`; parent/student demo accounts register through a seeded `BetaInviteCode`; seed timing measured over the public internet (I/O-bound, ~700 round trips).
- Known limits: `DemoSeeder` has no unit tests (exercised end to end on QA); `OutboundEmail.Attempts` stays 0 on a first-try success (cosmetic); `IepVersions` trigger reject path not exercised on QA (table empty).
- Subagents on Sonnet; the phase-4 docs/marketing slice was done by the coordinator.

