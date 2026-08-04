# Production Launch Checklist — School Self-Serve Signup

**Date:** 2026-06-07 · **Branch:** `feat/school-self-serve-signup` · **Plan:** `docs/plans/2026-06-06-001-feat-school-self-serve-signup-and-org-management-plan.md`

## Azure App Service application settings (API)

Required (new or changed by this work):
- [ ] `Email__ConnectionString` — Azure Communication Services connection string (staff/parent/student invite emails now matter commercially). Without it, emails are silently logged-not-sent.
- [ ] `Email__SenderAddress` — verified sender (default `DoNotReply@mail.iep-advisor.com`; ACS managed domain works for first sends)
- [ ] `App__FrontendUrl` — the real web origin (invite links are built from this; default is `http://localhost:5173` — wrong in prod)
- [ ] Confirm `Email__ExposeLinksForTesting` is **absent** (triple-gated to Development anyway, but keep config clean)
- [ ] **No `Feature__*` settings needed anymore** — the flag system was removed in P7; remove any stale ones
- [ ] Existing settings audit: `ConnectionStrings__DefaultConnection`, `ConnectionStrings__BlobStorage`, `Jwt__*`, `Anthropic__*`, `Cors` origins include the prod web origin

## Database / migrations

- [ ] This branch adds 4 migrations: `StaffProfileOrgRolesAndWipe` (**destructive**: wipes org data, renames TeacherProfiles→StaffProfiles, resets Educator/Student users to Parent — approved; no prod org data exists), `SchoolIsActive`, `AddStaffInvite`, `PendingStaffInviteUniqueEmail`
- [x] Full chain verified on real SQL Server (QA DB `sh-qa…/iep-assistant`, applied on startup 2026-06-07; register-district smoke + 77-test e2e run exercised the new schema)
- [ ] Note: the wipe migration deactivated/affected pre-existing QA staff accounts (e.g. `brad@sht.dev` needed reactivation) — expect the same on any environment with old educator data

## Verified working (2026-06-07, local against QA DB)

- [x] Backend suite: **235/235** green
- [x] `vite build` — **fixed** (esbuild reinstall); first successful production build of the rework
- [x] Playwright: **10/10 new e2e specs** green (district signup golden path, staff invite accept + negative paths, org scoping, deactivation/401, last-admin guard, roster scoping)
- [x] Full e2e suite: 67 passed / 8 failed — all 8 are **pre-existing test drift** in parent specs (tab-navigation in `advocacy-goals`/`iep-documents` page objects; nonexistent `dashboard-subscription` testid). Follow-up, not blockers.

## Security posture (agent-smith audit 2026-06-07: 0 critical)

- [x] H-1 fixed: `UseForwardedHeaders` + dedicated `register-district` rate limit (3/hour)
- [x] M-2 fixed: filtered unique index on pending staff invites
- [ ] Accepted/documented: email-enumeration on registration (bounded by rate limit), duplicate district names allowed (by design), last-admin concurrent-deactivation race (recoverable)
- [x] Dev secrets verified NOT in git: `appsettings.Development.json` is gitignored (`appsettings.*.json`); an earlier research note claiming committed secrets was incorrect. No rotation forced by git history.

## Carried pre-launch items (from the rework, still open)

- [ ] DB trigger/DENY for true IepVersion immutability (EF interceptor misses raw SQL)
- [ ] Pre-existing e2e test-drift fixes (8 failing specs above)

## Post-deploy validation (first district signup)

- Watch Serilog/Elastic for: `register-district` 429s (limiter working), `Email sent` vs `Email would be sent` (ACS configured), 401s with "Token has been revoked" (SecurityStamp churn)
- Healthy signals: District+StaffProfile rows created together; staff invites show `AcceptedAt` within days
- Failure signal/rollback trigger: any 500 on `/api/auth/register-district` or `/api/staff-invites/accept` — both are fully transactional, no partial-state cleanup needed
