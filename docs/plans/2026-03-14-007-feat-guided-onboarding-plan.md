---
title: "feat: Guided Onboarding — Walkthrough + IEP 101"
type: feat
status: completed
date: 2026-03-14
origin: docs/brainstorms/2026-03-14-platform-features-brainstorm.md
---

# feat: Guided Onboarding — Walkthrough + IEP 101

## Overview

A multi-step guided walkthrough for first-time users that introduces the platform, captures their state/jurisdiction, and guides them through adding their first child. Plus a standalone "IEP 101" reference page explaining IEP basics in plain language. The walkthrough ensures new parents don't land on an empty dashboard and feel lost.

(see brainstorm: `docs/brainstorms/2026-03-14-platform-features-brainstorm.md`, Feature 6)

## Problem Statement / Motivation

New parents registering for IEP Advisor currently land on an empty dashboard with no guidance. They may not know what an IEP is, what this platform does, or what to do first. The brainstorm specifies: "Step-by-step first-time flow, contextual tooltips, IEP 101 reference section, progressive disclosure — don't overwhelm new users."

## Proposed Solution

### Two capabilities:

1. **Guided Walkthrough** — A multi-step flow that appears after first login. Steps: Welcome → Set State → Add Child → What's Next. Skippable but encouraged. Completion tracked via `OnboardingCompletedAt` on the User entity.

2. **IEP 101 Page** — A standalone reference page accessible from the sidebar: "What is an IEP?", key terms glossary, your rights as a parent, what to expect at an IEP meeting. Written in brand voice (empowering, plain-spoken, parent-first).

### Walkthrough Steps

**Step 1: Welcome**
- "Welcome to IEP Advisor" heading (Lora)
- Brief 2-3 sentence explanation of what the platform does
- "Let's get you set up" CTA

**Step 2: Set Your State**
- Why it matters: "Your state determines which IEP laws apply"
- State selector (reuse existing `StateSelector` component)
- Saves to profile via existing `PUT /api/auth/me` endpoint
- "Skip for now" option

**Step 3: Add Your First Child**
- Embedded `ChildForm` (reuse existing component)
- On success, continues to next step
- "Skip for now" option

**Step 4: What's Next**
- Quick overview of platform features with Lucide icons:
  - Upload an IEP → get AI analysis
  - Set advocacy goals → check if IEP addresses them
  - Prep for meetings → get actionable checklists
  - Compare IEP versions → track changes over time
- "Go to Dashboard" CTA
- Link to "IEP 101" for parents new to IEPs

### Data Model Change

Add one field to User entity:

```csharp
public DateTime? OnboardingCompletedAt { get; set; }
```

When the walkthrough is completed (or skipped), this is set to `DateTime.UtcNow`. The dashboard checks this field — if null, redirect to onboarding.

### API Change

Update `PUT /api/auth/me` to accept and return `onboardingCompletedAt`. Or add a simple `POST /api/auth/complete-onboarding` endpoint.

### IEP 101 Content

Static page with brand styling. Sections:
1. **What is an IEP?** — Plain-language explanation
2. **Who gets an IEP?** — Eligibility basics (IDEA, 13 disability categories)
3. **What's in an IEP?** — Sections overview (present levels, goals, services, accommodations, placement)
4. **Your Rights as a Parent** — Key IDEA rights (participation, prior written notice, consent, dispute resolution)
5. **What to Expect at an IEP Meeting** — Meeting flow, who attends, how to prepare
6. **Glossary** — Common terms (LRE, FAPE, Related Services, Transition, etc.)

All content written in brand voice: empowering, clear, parent-first.

## Technical Approach

### Phase 1: Backend — User entity + endpoint

**Modified files:**

| File | Change |
|------|--------|
| `api/IepAssistant.Domain/Entities/User.cs` | Add `OnboardingCompletedAt` field |
| `api/IepAssistant.Services/Implementations/AuthService.cs` | Add `CompleteOnboardingAsync` method |
| `api/IepAssistant.Services/Interfaces/IAuthService.cs` | Add method signature |
| `api/IepAssistant.Api/Controllers/AuthController.cs` | Add `POST api/auth/complete-onboarding` endpoint |
| `api/IepAssistant.Api/DTOs/Auth/LoginResponse.cs` | Add `onboardingCompleted` field |

**EF Migration:** `AddOnboardingField`

### Phase 2: Frontend — Walkthrough

**New files:**

| File | Description |
|------|-------------|
| `web/src/features/onboarding/components/onboarding-flow.tsx` | Multi-step container with progress indicator |
| `web/src/features/onboarding/components/welcome-step.tsx` | Step 1: Welcome message |
| `web/src/features/onboarding/components/state-step.tsx` | Step 2: State selector |
| `web/src/features/onboarding/components/child-step.tsx` | Step 3: Add child form |
| `web/src/features/onboarding/components/next-steps.tsx` | Step 4: Feature overview |

**Modified files:**

| File | Change |
|------|--------|
| `web/src/types/api.ts` | Add `onboardingCompletedAt` to User type |
| `web/src/features/auth/stores/auth-context.tsx` | Check onboarding status, provide `completeOnboarding` function |
| `web/src/features/auth/api/auth-api.ts` | Add `completeOnboarding()` API call |
| `web/src/app/routes.tsx` | Add `/onboarding` route, redirect logic |
| `web/src/features/auth/components/dashboard-page.tsx` | Redirect to onboarding if not completed |
| `web/src/components/layouts/sidebar.tsx` | Add "IEP 101" nav link |

### Phase 3: IEP 101 Page

**New files:**

| File | Description |
|------|-------------|
| `web/src/features/onboarding/components/iep-101-page.tsx` | Static educational page with brand styling |

**Walkthrough UI design:**

```
┌─────────────────────────────────────────────┐
│         ○ ● ○ ○   Step 2 of 4              │
│                                              │
│     Set Your State                           │
│                                              │
│  Your state determines which IEP laws        │
│  apply to your child's education.            │
│                                              │
│     [State Selector Dropdown]                │
│                                              │
│  [ Skip for now ]        [ Continue → ]      │
└─────────────────────────────────────────────┘
```

- Progress dots at top (brand-teal-500 for current/completed, brand-slate-200 for pending)
- Centered card layout on brand-slate-50 background
- Lora headings, DM Sans body
- Brand Button primary for "Continue", ghost for "Skip"

## Acceptance Criteria

### Functional Requirements

- [ ] New users see the onboarding walkthrough after first login
- [ ] Walkthrough has 4 steps: Welcome, Set State, Add Child, What's Next
- [ ] Each step is skippable except Welcome
- [ ] State selector saves to user profile immediately
- [ ] Child form creates a real child profile (reuses existing form)
- [ ] Completing or skipping sets `OnboardingCompletedAt` on the user
- [ ] Subsequent logins go directly to dashboard (no repeat onboarding)
- [ ] IEP 101 page accessible from sidebar navigation
- [ ] IEP 101 contains: What is an IEP, Who gets one, What's in it, Your Rights, Meeting expectations, Glossary
- [ ] All content written in brand voice (empowering, plain-spoken)
- [ ] Progress dots show current step

### Non-Functional Requirements

- [ ] Brand UI components used throughout
- [ ] Onboarding works on mobile (responsive)
- [ ] IEP 101 page loads instantly (no API calls — static content)
- [ ] All new endpoints have `[Authorize]`

## Dependencies & Risks

**Dependencies:** None — uses existing state selector, child form, and auth endpoints

**Risks:**
- Content quality: IEP 101 content must be accurate and helpful. Review by someone with IEP domain expertise is recommended before launch.
- Existing users: users who registered before this feature won't have `OnboardingCompletedAt` set. They should NOT be forced through onboarding — set a default or check for existing children as a proxy.

## Sources & References

### Origin
- **Brainstorm:** [docs/brainstorms/2026-03-14-platform-features-brainstorm.md](docs/brainstorms/2026-03-14-platform-features-brainstorm.md) — Feature 6: Guided Onboarding. Key decisions: progressive disclosure, ensure state captured, IEP 101 reference section.

### Internal References
- State selector: `web/src/features/auth/components/state-selector.tsx`
- Child form: `web/src/features/children/components/child-form.tsx`
- Auth context: `web/src/features/auth/stores/auth-context.tsx`
- User entity: `api/IepAssistant.Domain/Entities/User.cs`
- Dashboard: `web/src/features/auth/components/dashboard-page.tsx`
- Sidebar: `web/src/components/layouts/sidebar.tsx`
