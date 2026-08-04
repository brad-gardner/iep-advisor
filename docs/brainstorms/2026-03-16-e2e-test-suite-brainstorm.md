---
name: E2E Test Suite
description: Playwright E2E tests covering critical user flows from TEST_PLAN.md
type: feature
---

# E2E Test Suite — Playwright Browser Tests

## What We're Building

A Playwright E2E test suite that automates the critical user flows from `docs/TEST_PLAN.md`. Tests run against either local dev (localhost:5200 frontend, localhost:7200 backend) or a deployed staging environment via a configurable base URL flag.

**Scope:** Core happy-path flows first (~20 tests), covering:
1. Registration with beta invite code
2. Login / logout
3. Onboarding walkthrough
4. Create child profile
5. Add advocacy goals
6. Create IEP event + upload PDF
7. Trigger and view IEP analysis
8. Generate meeting prep checklist
9. Knowledge base search
10. Profile management

## Why This Approach

- **Playwright over Cypress:** Playwright has better multi-browser support, auto-wait, built-in test generator, and runs headed/headless. It's the modern standard.
- **E2E over unit tests:** The test plan is written from a user perspective. E2E tests directly validate what users experience. Backend logic is already validated through the E2E flows.
- **Critical flows first:** ~20 tests covering the core happy path catches the most regressions with the least effort. Can expand later.

## Key Decisions

1. **Playwright** as the test framework (installed in `web/` or a separate `e2e/` directory)
2. **Configurable base URL** — default `http://localhost:5200`, override via `BASE_URL` env var for staging
3. **Test data setup** — tests create their own data (register a fresh user, create a child, etc.) rather than relying on seeded data. Each test run is independent.
4. **Beta invite code** — tests need a valid invite code. Options: pre-generate codes in test setup via admin API, or use a test-only endpoint. Simplest: generate a code via `POST /api/admin/beta-codes` using admin credentials at the start of the test suite.
5. **No parallel execution initially** — run tests sequentially to avoid data conflicts. Can parallelize later with isolated test users.
6. **Screenshot on failure** — Playwright captures screenshots automatically on test failure for debugging.

## Test Structure

```
e2e/
  playwright.config.ts       — config with base URL, timeouts, retries
  tests/
    auth.spec.ts             — register, login, logout, password reset
    onboarding.spec.ts       — 4-step walkthrough
    children.spec.ts         — create, edit, delete child
    advocacy-goals.spec.ts   — add, edit, delete, reorder goals
    iep-documents.spec.ts    — create IEP event, upload PDF
    iep-analysis.spec.ts     — trigger analysis, view results
    meeting-prep.spec.ts     — generate checklist, check items
    knowledge-base.spec.ts   — search, filter categories
    profile.spec.ts          — edit profile, state selection
    sharing.spec.ts          — invite, accept, viewer permissions
  helpers/
    auth.ts                  — login helper, get admin token
    setup.ts                 — generate beta code, create test user
```

## Open Questions

*None — resolved during brainstorming.*
