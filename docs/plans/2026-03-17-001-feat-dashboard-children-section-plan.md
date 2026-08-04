---
title: "feat: Add Children Section to Dashboard"
type: feat
status: completed
date: 2026-03-17
---

# feat: Add Children Section to Dashboard

## Overview

Add a "My Children" section to the dashboard that shows a summary of the user's child profiles with quick navigation to detail pages. This replaces the current minimal "Manage profiles" link in the Account card with a proper, visible section.

## Problem Statement / Motivation

The dashboard currently buries children behind a text link inside the "Your Account" card. Children are the core entity of the platform — parents need to see them front and center when they log in. A visible children section improves discoverability and creates a natural starting point for the IEP workflow.

## Proposed Solution

Create a `DashboardChildrenSection` component that:
- Fetches children using the existing `useChildren()` hook
- Displays up to 4 children in a responsive card grid
- Shows a "View all" link when more than 4 exist
- Includes an "Add child" CTA in the section header
- Handles empty, loading, and error states
- Renders between the onboarding/warning notices and the subscription card

### Placement in Dashboard

```
Welcome heading
Onboarding banner (conditional)
Missing state warning (conditional)
━━━ NEW: My Children section ━━━
Subscription status card
Your Account card (remove children link from here)
Quick Actions card
```

## Technical Considerations

### No Backend Changes

The `GET /api/children` endpoint already returns everything needed. No new API, no schema changes.

### Component Structure

One new component following existing patterns:

```
web/src/features/children/components/dashboard-children-section.tsx
```

This follows the feature-based folder structure — the component lives in `features/children/` since it's a children concern rendered on the dashboard, not a dashboard concern.

### Card Display Fields

Each child card shows:
- **Heading:** `firstName lastName` (truncate with ellipsis if long)
- **Badge:** `<SharedBadge role={role} />` if `role !== 'owner'`
- **Metadata chips:** `gradeLevel`, `schoolDistrict` (only if present)
- Entire card is clickable, links to `/children/:id`

Omit `disabilityCategory` and `dateOfBirth` from the dashboard summary — these are sensitive/detailed fields better suited to the detail page.

### States

| State | Rendering |
|-------|-----------|
| Loading | Spinner (matches existing dashboard pattern) |
| Error | `<Notice variant="error">` with retry button |
| Empty (0 children) | Compact card with icon + "Add your first child" CTA → `/children/new` |
| 1-4 children | Full grid, `grid-cols-1 md:grid-cols-2 gap-4` |
| 5+ children | Show first 4 + "View all (N)" link → `/children` |

### Sort Order

Owned children first, then shared, each group alphabetical by `firstName`. Sort client-side since the list is small.

## Acceptance Criteria

- [x] Dashboard renders a "My Children" section with `h2` heading
- [x] Section appears below notices and above the subscription card
- [x] Each child renders as a clickable `<Card>` linking to `/children/:id`
- [x] Cards show name, shared badge (if applicable), grade level, and school district
- [x] Empty state shows icon + text + "Add your first child profile" button → `/children/new`
- [x] Loading state shows spinner
- [x] Error state shows `<Notice variant="error">` with retry affordance
- [x] Section header includes "Add" link → `/children/new` (visible when children exist)
- [x] Maximum 4 children displayed; "View all (N)" link when more exist
- [x] Owned children sort before shared children
- [x] Remove the children link from the "Your Account" card (replaced by this section)
- [x] `data-testid="dashboard-children-section"` on the section wrapper
- [x] `data-testid="dashboard-child-card"` on each child card

## MVP

### dashboard-children-section.tsx

```tsx
import { Link } from 'react-router-dom';
import { Users, Plus } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { SharedBadge } from '@/features/children/components/shared-badge';
import { useChildren } from '@/features/children/hooks/use-children';
import type { ChildProfile } from '@/types/api';

const MAX_DISPLAY = 4;

function sortChildren(children: ChildProfile[]): ChildProfile[] {
  return [...children].sort((a, b) => {
    if (a.role === 'owner' && b.role !== 'owner') return -1;
    if (a.role !== 'owner' && b.role === 'owner') return 1;
    return a.firstName.localeCompare(b.firstName);
  });
}

export function DashboardChildrenSection() {
  const { children, isLoading, error, reload } = useChildren();

  if (isLoading) {
    return (
      <section data-testid="dashboard-children-section">
        <h2 className="font-serif text-lg text-brand-slate-800 mb-4">My Children</h2>
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
        </div>
      </section>
    );
  }

  if (error) {
    return (
      <section data-testid="dashboard-children-section">
        <h2 className="font-serif text-lg text-brand-slate-800 mb-4">My Children</h2>
        <Notice variant="error" title="Couldn't load children">
          <button onClick={reload} className="text-sm underline">Try again</button>
        </Notice>
      </section>
    );
  }

  if (!children.length) {
    return (
      <section data-testid="dashboard-children-section">
        <h2 className="font-serif text-lg text-brand-slate-800 mb-4">My Children</h2>
        <Card className="text-center py-12">
          <Users className="mx-auto h-12 w-12 text-brand-slate-300" strokeWidth={1.8} />
          <p className="mt-3 text-sm text-brand-slate-400">No child profiles yet</p>
          <Link to="/children/new" className="mt-4 inline-block">
            <Button variant="primary" size="sm">Add your first child profile</Button>
          </Link>
        </Card>
      </section>
    );
  }

  const sorted = sortChildren(children);
  const displayed = sorted.slice(0, MAX_DISPLAY);
  const hasMore = children.length > MAX_DISPLAY;

  return (
    <section data-testid="dashboard-children-section">
      <div className="flex items-center justify-between mb-4">
        <h2 className="font-serif text-lg text-brand-slate-800">My Children</h2>
        <Link to="/children/new" className="flex items-center gap-1 text-sm text-brand-teal-500 hover:text-brand-teal-400">
          <Plus className="h-4 w-4" strokeWidth={1.8} />
          Add
        </Link>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {displayed.map((child) => (
          <Link key={child.id} to={`/children/${child.id}`} className="block">
            <Card
              className="hover:border-brand-teal-200 transition-colors"
              data-testid="dashboard-child-card"
            >
              <div className="flex items-center gap-2">
                <h3 className="font-serif text-brand-slate-800 truncate">
                  {child.firstName} {child.lastName}
                </h3>
                {child.role !== 'owner' && <SharedBadge role={child.role} />}
              </div>
              {(child.gradeLevel || child.schoolDistrict) && (
                <div className="mt-2 flex flex-wrap gap-3 text-xs text-brand-slate-400">
                  {child.gradeLevel && <span>Grade: {child.gradeLevel}</span>}
                  {child.schoolDistrict && <span>{child.schoolDistrict}</span>}
                </div>
              )}
            </Card>
          </Link>
        ))}
      </div>
      {hasMore && (
        <Link
          to="/children"
          className="mt-3 block text-sm text-brand-teal-500 hover:text-brand-teal-400"
        >
          View all ({children.length})
        </Link>
      )}
    </section>
  );
}
```

### dashboard-page.tsx changes

```tsx
// Add import
import { DashboardChildrenSection } from '@/features/children/components/dashboard-children-section';

// Insert <DashboardChildrenSection /> after the notices, before SubscriptionStatusCard
// Remove the children link tile from the "Your Account" card
```

## Dependencies & Risks

- **useChildren hook may need `error` exposed** — currently errors are caught silently. Check if the hook returns an error state; if not, add it (minor change).
- **No backend changes** — zero risk to API or database.
- **E2E tests** — existing dashboard tests may need updating if they assert on the "Your Account" card structure (the children link is being removed from it).

## Sources & References

- Existing children list pattern: `web/src/features/children/components/children-list-page.tsx`
- Dashboard page: `web/src/features/auth/components/dashboard-page.tsx`
- useChildren hook: `web/src/features/children/hooks/use-children.ts`
- Shared badge component: `web/src/features/children/components/shared-badge.tsx`
- UI components: `web/src/components/ui/` (Card, Button, Notice)
