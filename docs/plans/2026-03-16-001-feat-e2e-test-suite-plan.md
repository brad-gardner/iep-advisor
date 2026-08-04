---
title: "feat: E2E Test Suite — Playwright Browser Tests for Critical User Flows"
type: feat
status: active
date: 2026-03-16
origin: docs/brainstorms/2026-03-16-e2e-test-suite-brainstorm.md
---

# feat: E2E Test Suite — Playwright Browser Tests for Critical User Flows

## Overview

Set up Playwright and write ~20 E2E tests covering the critical user flows from `docs/TEST_PLAN.md`. Tests run against local dev (localhost:5200 frontend, localhost:7200 backend) by default, with a configurable base URL for staging/production.

(see brainstorm: `docs/brainstorms/2026-03-16-e2e-test-suite-brainstorm.md`)

## Proposed Solution

### Project Structure

```
e2e/
  package.json                  — Playwright + dependencies
  playwright.config.ts          — config: base URL, timeouts, screenshots
  .env.example                  — BASE_URL, ADMIN_EMAIL, ADMIN_PASSWORD
  tests/
    auth.spec.ts                — register, login, logout
    onboarding.spec.ts          — 4-step walkthrough
    children.spec.ts            — create, edit child
    advocacy-goals.spec.ts      — CRUD + reorder
    iep-documents.spec.ts       — create IEP event, upload PDF
    iep-analysis.spec.ts        — trigger analysis, view results
    meeting-prep.spec.ts        — generate checklist, check items
    knowledge-base.spec.ts      — search, category filter
    profile.spec.ts             — edit profile, state
    smoke.spec.ts               — every page loads without error
  helpers/
    auth.ts                     — login helper, admin API calls
    test-data.ts                — generate beta code, create test user
    selectors.ts                — common CSS selectors / test IDs
```

### Configuration

```typescript
// playwright.config.ts
export default defineConfig({
  testDir: './tests',
  baseURL: process.env.BASE_URL || 'http://localhost:5200',
  timeout: 30_000,
  retries: process.env.CI ? 2 : 0,
  use: {
    screenshot: 'only-on-failure',
    trace: 'on-first-retry',
    headless: true,
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
});
```

### Test Data Strategy

Each test run is self-contained:
1. **Global setup** (`globalSetup.ts`): calls `POST /api/admin/beta-codes` with admin credentials to generate a fresh invite code
2. **Auth helper**: registers a unique test user with that code, logs in, stores auth state
3. **Per-test cleanup**: tests create their own data (children, goals, IEPs) — no shared state between tests
4. **Unique identifiers**: test user email uses `test-{timestamp}@e2e.test` to avoid collisions

### Environment Variables

```
BASE_URL=http://localhost:5200          # Frontend URL
API_URL=http://localhost:7200           # Backend URL (for direct API calls in setup)
ADMIN_EMAIL=admin@example.com           # Admin account for generating beta codes
ADMIN_PASSWORD=adminpassword            # Admin password
```

## Technical Approach

### Phase 1: Setup Playwright + Helpers + Smoke Tests

**New files:**

| File | Description |
|------|-------------|
| `e2e/package.json` | Playwright dependency + scripts |
| `e2e/playwright.config.ts` | Config with base URL, timeouts, screenshot on failure |
| `e2e/.env.example` | Environment variable template |
| `e2e/helpers/auth.ts` | `loginAsAdmin()`, `loginAsUser()`, `registerTestUser()` |
| `e2e/helpers/test-data.ts` | `generateBetaCode()`, `createTestChild()`, `createTestIep()` |
| `e2e/tests/smoke.spec.ts` | Verify every page loads: dashboard, children, profile, knowledge-base, IEP 101, subscription |
| `e2e/global-setup.ts` | Generate beta code + register test user before all tests |

### Phase 2: Auth Flow Tests

| File | Tests |
|------|-------|
| `e2e/tests/auth.spec.ts` | Register with valid code, register with invalid code (error), login with valid credentials, login with wrong password (error), logout, forgot password flow |

### Phase 3: Core User Journey Tests

| File | Tests |
|------|-------|
| `e2e/tests/onboarding.spec.ts` | Complete all 4 steps, skip steps, verify completion |
| `e2e/tests/children.spec.ts` | Create child with all fields, edit child, view child detail |
| `e2e/tests/advocacy-goals.spec.ts` | Add goal, edit goal, delete goal, verify 10-goal limit message |
| `e2e/tests/iep-documents.spec.ts` | Create IEP event with metadata, upload PDF, verify processing |
| `e2e/tests/profile.spec.ts` | Update name, change state, verify save |
| `e2e/tests/knowledge-base.spec.ts` | Search for "FAPE", filter by category, verify results |

### Phase 4: AI Feature Tests (longer running)

| File | Tests |
|------|-------|
| `e2e/tests/iep-analysis.spec.ts` | Trigger analysis, wait for completion (polling), verify overview/goals/red flags displayed |
| `e2e/tests/meeting-prep.spec.ts` | Generate checklist, verify sections appear, check off an item, verify persistence |

### Scripts

```json
// e2e/package.json scripts
{
  "test": "playwright test",
  "test:headed": "playwright test --headed",
  "test:ui": "playwright test --ui",
  "test:staging": "BASE_URL=https://your-staging-url.com playwright test"
}
```

## Test List (~20 tests)

| # | File | Test Name | Test Plan Ref |
|---|------|-----------|---------------|
| 1 | smoke.spec.ts | every protected page loads | 16.6 |
| 2 | auth.spec.ts | register with valid beta code | 1.2.5 |
| 3 | auth.spec.ts | register with invalid code shows error | 1.2.4 |
| 4 | auth.spec.ts | login with valid credentials | 1.3.1 |
| 5 | auth.spec.ts | login with wrong password shows error | 1.3.2 |
| 6 | auth.spec.ts | logout clears session | 1.3.1 |
| 7 | onboarding.spec.ts | complete all 4 onboarding steps | 2.2-2.10 |
| 8 | children.spec.ts | create child with all fields | 4.1.4 |
| 9 | children.spec.ts | edit child profile | 4.2.3 |
| 10 | advocacy-goals.spec.ts | add advocacy goal with category | 5.3 |
| 11 | advocacy-goals.spec.ts | edit existing goal | 5.6 |
| 12 | advocacy-goals.spec.ts | delete goal with confirmation | 5.7 |
| 13 | iep-documents.spec.ts | create IEP event with date + type | 6.1.2 |
| 14 | iep-documents.spec.ts | upload PDF to existing IEP | 6.2.2 |
| 15 | iep-analysis.spec.ts | trigger analysis and view results | 7.1-7.3 |
| 16 | iep-analysis.spec.ts | gap analysis shows when goals exist | 7.7 |
| 17 | meeting-prep.spec.ts | generate checklist from goals | 8.2.1-8.2.2 |
| 18 | meeting-prep.spec.ts | check off item persists | 8.1.4-8.1.5 |
| 19 | knowledge-base.spec.ts | search returns results | 12.3 |
| 20 | profile.spec.ts | update name and state | 3.2-3.3 |

## Acceptance Criteria

- [ ] Playwright installed in `e2e/` directory with config
- [ ] Tests run via `npm test` from `e2e/` directory
- [ ] Configurable base URL via `BASE_URL` env var
- [ ] Global setup generates beta code + registers test user
- [ ] Auth helper handles login/logout for tests
- [ ] Smoke tests verify every main page loads
- [ ] Auth flow tests: register, login, logout, error cases
- [ ] Child management tests: create, edit
- [ ] Advocacy goals tests: add, edit, delete
- [ ] IEP document tests: create event, upload PDF
- [ ] Analysis test: trigger + verify results (with polling for async completion)
- [ ] Meeting prep test: generate + check items
- [ ] Knowledge base test: search + filter
- [ ] Profile test: update name + state
- [ ] Screenshots captured on test failure
- [ ] All tests pass against local dev environment

## Dependencies & Risks

**Dependencies:**
- Running local dev servers (frontend on 5200, backend on 7200)
- An admin account in the database for beta code generation
- A sample PDF file for upload tests

**Risks:**
- AI analysis tests are slow (~30-60 seconds) — need generous timeouts and polling
- Tests depend on Claude API being available for analysis/meeting prep tests
- File upload in Playwright requires `setInputFiles` — need to handle the drag-and-drop zone

## Sources & References

### Origin
- **Brainstorm:** [docs/brainstorms/2026-03-16-e2e-test-suite-brainstorm.md](docs/brainstorms/2026-03-16-e2e-test-suite-brainstorm.md) — Key decisions: Playwright, configurable base URL, critical flows first, self-contained test data.

### Internal References
- Test plan: `docs/TEST_PLAN.md`
- Frontend routes: `web/src/app/routes.tsx`
- Local dev ports: frontend 5200, backend 7200
