---
title: "feat: Brand Redesign with Sidebar Layout"
type: feat
status: active
date: 2026-03-14
---

# feat: Brand Redesign with Sidebar Layout

## Overview

Complete frontend redesign to apply the IEP Advisor brand guidelines (Refined Trust theme) across every page, replace the current top-nav layout with a modern left sidebar navigation, add the brand logo lockup, and extract shared UI primitives. This gets the app ready for real users.

## Problem Statement / Motivation

The current frontend uses stock Tailwind defaults (blue-600 primary, gray neutrals, Inter font) with no design system, no shared components, and a minimal top-bar navigation. It looks like a developer prototype, not a product parents would trust with their child's education data. The brand guidelines define a polished, trustworthy visual identity that needs to be implemented.

## Proposed Solution

### Phase 1: Foundation — Design System & Shared Primitives

Set up the design tokens, fonts, and shared UI components that everything else builds on.

#### 1a. Tailwind Theme Configuration

Update `web/tailwind.config.js` with the full brand color palette:

```js
// web/tailwind.config.js
theme: {
  extend: {
    colors: {
      brand: {
        teal: {
          50: '#EDF8F5',
          100: '#C8EEE6',
          200: '#A3E4D7',
          300: '#6DD4C0',
          400: '#3BC4A9',
          500: '#1A9478',
          600: '#0F6652',
          700: '#0A4D3E',
          800: '#06342A',
        },
        amber: {
          50: '#FEF7EC',
          100: '#FCE8C0',
          200: '#F9D98A',
          300: '#F0C050',
          400: '#D4820F',
          500: '#A8620A',
          600: '#7C4808',
        },
        slate: {
          50: '#F5F7F7',
          100: '#E8ECEC',
          200: '#D1D8D8',
          300: '#A8B5B5',
          400: '#7F9292',
          500: '#5A6F6F',
          600: '#3E5252',
          700: '#2C3C3C',
          800: '#1E2A2A',
        },
      },
    },
    fontFamily: {
      serif: ['Lora', 'Georgia', 'serif'],
      sans: ['DM Sans', 'system-ui', 'sans-serif'],
    },
    borderRadius: {
      input: '4px',
      badge: '6px',
      button: '8px',
      card: '12px',
      modal: '16px',
    },
  },
},
```

#### 1b. Font Loading

Add Google Fonts to `web/index.html`:

```html
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=DM+Sans:wght@300;400;500;600&family=Lora:wght@400;600;700&display=swap" rel="stylesheet">
```

Update `web/src/index.css`:
- Set `font-family: 'DM Sans', system-ui, sans-serif` as body default
- Add heading styles using Lora
- Define CSS custom properties for semantic color tokens

#### 1c. Icon Library

Install Lucide React (brand guidelines specify Lucide outline icons at 1.8px stroke):

```bash
cd web && npm install lucide-react
```

#### 1d. Shared UI Components

Create `web/src/components/ui/` with brand-compliant primitives:

| Component | File | Spec |
|-----------|------|------|
| `Button` | `button.tsx` | Primary (teal fill), Secondary (teal outline 1.5px), Amber CTA, Ghost. 8px radius, DM Sans 13px/500 |
| `Input` | `input.tsx` | 4px radius, 0.5px slate-200 border, teal focus ring (1px + 3px glow), DM Sans 14px |
| `Card` | `card.tsx` | 12px radius, 0.5px slate-200 border, white bg, no shadow |
| `Badge` | `badge.tsx` | 6px radius, semantic colors (success/warning/error/info per token table) |
| `Notice` | `notice.tsx` | Info (teal) and Warning (amber) alert boxes with icon + title + description |
| `Logo` | `logo.tsx` | SVG logo lockup: teal checkmark circle + "IEP" (Lora regular) + "Advisor" (Lora semibold teal) + "NAVIGATE WITH CONFIDENCE" eyebrow |

### Phase 2: Layout — Sidebar Navigation

Replace the top-nav `MainLayout` with a sidebar layout.

#### New `MainLayout` Structure

```
┌──────────────────────────────────────────────┐
│ ┌──────────┐ ┌────────────────────────────┐  │
│ │           │ │ Header (breadcrumb + user) │  │
│ │  Sidebar  │ ├────────────────────────────┤  │
│ │           │ │                            │  │
│ │  Logo     │ │     Page Content           │  │
│ │  Nav      │ │                            │  │
│ │  items    │ │                            │  │
│ │           │ │                            │  │
│ │  User     │ │                            │  │
│ │  footer   │ │                            │  │
│ └──────────┘ └────────────────────────────┘  │
└──────────────────────────────────────────────┘
```

**Sidebar** (`web/src/components/layouts/sidebar.tsx`):
- Width: `w-64` (256px)
- Background: `bg-brand-slate-800` (dark slate per brand)
- Logo lockup at top (light variant — white text on dark bg)
- Nav items with Lucide icons:
  - Dashboard (LayoutDashboard)
  - My Children (Users)
  - Profile (UserCircle)
- Active state: teal left border accent (2px) + teal text
- Inactive: slate-400 text, hover slate-200
- Bottom section: user name + "Sign Out" link
- Mobile: collapsible hamburger menu (slide-over)

**Header** (`web/src/components/layouts/header.tsx`):
- Slim bar above content area
- Shows page title / breadcrumb
- Right side: user avatar placeholder or initials

**Updated `MainLayout`** (`web/src/components/layouts/main-layout.tsx`):
- Flex row: Sidebar (fixed) + Content area (scrollable)
- Content area: `bg-brand-slate-50` background
- Max content width: `max-w-5xl` with comfortable padding

#### Updated `AuthLayout`

- Full-screen centered with `bg-brand-slate-800` background
- Logo lockup (light variant) centered above form card
- Form card: white bg, 12px radius, brand styling
- Tagline "Navigate with confidence" below logo

### Phase 3: Page-by-Page Redesign

Every page gets restyled with brand tokens. The list below covers each page and what changes.

#### Auth Pages

**LoginPage** (`web/src/features/auth/components/login-page.tsx`)
- H1: "Welcome Back" in Lora 32px/600
- Form inputs: brand Input component (4px radius, teal focus)
- Primary button: brand Button (teal, 8px radius)
- "Create account" link in teal

**RegisterPage** (`web/src/features/auth/components/register-page.tsx`)
- Same treatment as login
- H1: "Create Your Account" in Lora

**DashboardPage** (`web/src/features/auth/components/dashboard-page.tsx`)
- H1: "Welcome, {firstName}" in Lora
- Quick action cards: brand Card + teal CTAs
- Overview stats in clean grid layout

**ProfilePage** (`web/src/features/auth/components/profile-page.tsx`)
- H1: "Your Profile" in Lora
- Form inputs: brand Input
- State selector: brand styling
- Save button: brand Button primary

#### Children Pages

**ChildrenListPage** (`web/src/features/children/components/children-list-page.tsx`)
- H1: "Your Children" in Lora
- Child cards: brand Card with 12px radius, slate borders
- "Add Child" CTA: brand Button primary (teal)

**CreateChildPage / ChildForm** (`web/src/features/children/components/create-child-page.tsx`, `child-form.tsx`)
- All inputs: brand Input component
- Labels: DM Sans 13px/500
- Submit: brand Button primary

**ChildDetailPage** (`web/src/features/children/components/child-detail-page.tsx`)
- H1: child name in Lora
- Profile section: brand Card
- Advocacy Goals section: brand Card with section heading in Lora H2
- IEP Documents section: brand Card

#### IEP Viewer Pages

**IepViewerPage** (`web/src/features/iep-documents/components/iep-viewer-page.tsx`)
- Section sidebar: use brand slate colors, active item has teal left accent border (2px)
- Tab bar: teal underline for active tab
- Processing banner: brand Warning notice (amber)
- Error banner: brand Error notice (red)

**Analysis Components** (all files in `web/src/features/iep-documents/components/`):
- `analysis-overview.tsx`: Lora headings, teal section dividers
- `analysis-goal-card.tsx`: Matches the brand "IEP Goal Card" component exactly — Lora heading, italic goal text, teal "What this means" panel with 2px left border, amber "Flag" panel with 2px left border
- `smart-criteria-grid.tsx`: Use semantic color tokens (green=success, amber=warning, red=error)
- `red-flag-card.tsx`: Use brand amber (warnings) and red (errors) semantic tokens
- `analysis-empty-state.tsx`: Brand teal CTA button
- `analysis-processing.tsx`: Teal spinner
- `advocacy-gap-analysis.tsx`: Teal success/amber partial/red not-addressed per semantic tokens
- `goal-alignment-card.tsx`: Brand semantic colors
- `stale-analysis-banner.tsx`: Brand amber warning notice

#### Advocacy Goals Components

- `advocacy-goal-card.tsx`: brand Card, teal accent category badges
- `advocacy-goal-form.tsx`: brand Input + brand Button
- `advocacy-goals-empty-state.tsx`: Lucide icon (teal), brand Button CTA
- `advocacy-goals-list.tsx`: brand layout

### Phase 4: Polish

- Responsive mobile sidebar (hamburger + slide-over)
- Loading spinners use teal
- Smooth transitions on nav hover/active states
- Ensure all focus states use the brand focus ring (1px teal-400 + 3px teal-50 glow)
- Update `<title>` in index.html to "IEP Advisor"
- Add favicon with brand teal checkmark

## Files Changed

### New Files (Phase 1 — Foundation)
| File | Description |
|------|-------------|
| `web/src/components/ui/button.tsx` | Brand Button component (Primary/Secondary/Amber/Ghost) |
| `web/src/components/ui/input.tsx` | Brand Input component |
| `web/src/components/ui/card.tsx` | Brand Card component |
| `web/src/components/ui/badge.tsx` | Brand Badge component |
| `web/src/components/ui/notice.tsx` | Brand Notice/Alert component |
| `web/src/components/ui/logo.tsx` | SVG Logo lockup component |

### New Files (Phase 2 — Layout)
| File | Description |
|------|-------------|
| `web/src/components/layouts/sidebar.tsx` | Left sidebar navigation |
| `web/src/components/layouts/header.tsx` | Slim content area header |

### Modified Files
| File | Change |
|------|--------|
| `web/tailwind.config.js` | Brand color palette, fonts, border radii |
| `web/index.html` | Google Fonts links, title change, favicon |
| `web/src/index.css` | Font family, heading styles, semantic tokens as CSS vars |
| `web/src/components/layouts/main-layout.tsx` | Sidebar layout (complete rewrite) |
| `web/src/components/layouts/auth-layout.tsx` | Brand styling + logo (complete rewrite) |
| `web/src/features/auth/components/login-page.tsx` | Brand components + typography |
| `web/src/features/auth/components/register-page.tsx` | Brand components + typography |
| `web/src/features/auth/components/dashboard-page.tsx` | Brand cards + typography |
| `web/src/features/auth/components/profile-page.tsx` | Brand inputs + typography |
| `web/src/features/auth/components/state-selector.tsx` | Brand Input styling |
| `web/src/features/children/components/children-list-page.tsx` | Brand cards + typography |
| `web/src/features/children/components/create-child-page.tsx` | Brand inputs |
| `web/src/features/children/components/child-form.tsx` | Brand Input component |
| `web/src/features/children/components/child-detail-page.tsx` | Brand cards + sections |
| `web/src/features/iep-documents/components/iep-viewer-page.tsx` | Brand sidebar nav + tabs |
| `web/src/features/iep-documents/components/analysis-tab.tsx` | Brand sidebar + semantic colors |
| `web/src/features/iep-documents/components/analysis-overview.tsx` | Brand typography + cards |
| `web/src/features/iep-documents/components/analysis-section-detail.tsx` | Brand typography |
| `web/src/features/iep-documents/components/analysis-goals-list.tsx` | Brand semantic colors |
| `web/src/features/iep-documents/components/analysis-goal-card.tsx` | Brand goal card (Lora heading, teal/amber panels per guidelines) |
| `web/src/features/iep-documents/components/analysis-empty-state.tsx` | Brand CTA |
| `web/src/features/iep-documents/components/analysis-processing.tsx` | Teal spinner |
| `web/src/features/iep-documents/components/red-flag-card.tsx` | Brand semantic tokens |
| `web/src/features/iep-documents/components/smart-criteria-grid.tsx` | Brand semantic colors |
| `web/src/features/iep-documents/components/iep-upload.tsx` | Brand styling |
| `web/src/features/iep-documents/components/iep-document-list.tsx` | Brand cards + badges |
| `web/src/features/iep-documents/components/advocacy-gap-analysis.tsx` | Brand semantic colors |
| `web/src/features/iep-documents/components/goal-alignment-card.tsx` | Brand semantic colors |
| `web/src/features/iep-documents/components/stale-analysis-banner.tsx` | Brand amber notice |
| `web/src/features/advocacy-goals/components/advocacy-goal-card.tsx` | Brand card + badges |
| `web/src/features/advocacy-goals/components/advocacy-goal-form.tsx` | Brand Input + Button |
| `web/src/features/advocacy-goals/components/advocacy-goals-list.tsx` | Brand layout |
| `web/src/features/advocacy-goals/components/advocacy-goals-empty-state.tsx` | Brand CTA + Lucide icon |
| `web/src/features/advocacy-goals/constants.ts` | Update category colors to brand tokens |

## Acceptance Criteria

### Functional Requirements

- [ ] Sidebar navigation with logo, nav links (Dashboard, Children, Profile), and sign-out
- [ ] Sidebar collapses to hamburger on mobile (<768px)
- [ ] Logo lockup displays correctly (teal icon + "IEP" regular + "Advisor" semibold teal + tagline)
- [ ] All pages use Lora for headings (H1: 32px, H2: 22px, H3: 17px)
- [ ] All body/UI text uses DM Sans (body: 14px, lead: 16px, labels: 13px, captions: 11px)
- [ ] Primary buttons use teal-500 fill with 8px radius
- [ ] Secondary buttons use teal-300 1.5px outline with 8px radius
- [ ] Warning/flag elements use amber-400
- [ ] Error elements use red (#B91C1C)
- [ ] Success elements use teal semantic tokens (bg #EDF8F5, text #0F6652, border #C8EEE6)
- [ ] Warning elements use amber semantic tokens (bg #FEF7EC, text #A8620A, border #FCE8C0)
- [ ] All cards use 12px radius, 0.5px slate-200 border, no shadow
- [ ] All form inputs use 4px radius, teal focus ring
- [ ] IEP goal cards match brand spec (eyebrow, Lora heading, italic goal text, teal "What this means" panel, amber flag panel)
- [ ] Auth pages show brand logo on dark slate background
- [ ] Every page title updated from "IEP Assistant" to "IEP Advisor"
- [ ] Lucide icons used throughout (1.8px stroke weight)

### Non-Functional Requirements

- [ ] No stock Tailwind blue-600 remaining in any component
- [ ] No inline color values — all from brand palette or semantic tokens
- [ ] Shared UI components used consistently (no duplicate button/input patterns)
- [ ] Google Fonts loaded with `display=swap` for performance
- [ ] All interactive elements have visible focus states per brand spec

## Dependencies & Risks

**Dependencies:**
- Google Fonts CDN (Lora + DM Sans)
- `lucide-react` npm package

**Risks:**
- High touch count (35+ files) — use incremental commits per phase
- IEP goal card component is the most complex brand component — implement carefully per the spec on p.5 of the guidelines
- Mobile sidebar needs testing across breakpoints

## Sources & References

### Brand Guidelines
- `docs/IEP_Advisor_Brand_Guidelines.pdf` — all specs referenced from this document

### Key Brand Specs Summary
| Spec | Value |
|------|-------|
| Primary color | Teal #1A9478 |
| Accent color | Amber #D4820F |
| Neutral color | Slate #1E2A2A |
| Error color | Red #B91C1C |
| Heading font | Lora (serif) 600 weight |
| Body font | DM Sans (sans) 400 weight |
| Button radius | 8px |
| Card radius | 12px |
| Input radius | 4px |
| Badge radius | 6px |
| Card border | 0.5px slate-200 |
| Focus ring | 1px teal-400 + 3px teal-50 glow |
| Featured accent | 2px teal-500 left border |
| Icon library | Lucide, 1.8px stroke |
| Spacing scale | 4, 8, 12, 16, 24, 32, 48, 64px |

### Internal References
- Current layout: `web/src/components/layouts/main-layout.tsx`
- Current auth layout: `web/src/components/layouts/auth-layout.tsx`
- Current tailwind config: `web/tailwind.config.js`
- Current CSS: `web/src/index.css`
- All component files listed in the Files Changed section above
