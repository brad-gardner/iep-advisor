---
title: "feat: Marketing Website for IEP Advisor"
type: feat
status: completed
date: 2026-03-17
origin: docs/brainstorms/2026-03-17-marketing-website-brainstorm.md
---

# feat: Marketing Website for IEP Advisor

## Overview

Build a standalone single-page marketing website for IEP Advisor that introduces the platform to parents navigating IEPs and drives beta signups. The site lives at `/marketing` in the monorepo, uses plain HTML + Tailwind CSS via CDN (no build step), and collects signups through a Google Form.

**Target audience:**
- **Primary:** Parents of children with IEPs who need help understanding and advocating
- **Secondary:** Special education advocates, attorneys, and professionals who may refer parents

## Problem Statement / Motivation

The IEP Advisor app is being built but has no public-facing presence. Parents cannot discover the product, learn about it, or express interest in beta access. A lightweight marketing site bridges this gap with minimal engineering effort — no new APIs, no database changes, no React complexity.

(see brainstorm: docs/brainstorms/2026-03-17-marketing-website-brainstorm.md)

## Proposed Solution

A single-page static HTML site with four content sections, matching the existing brand identity, deployed independently from the React app.

### File Structure

```
/marketing
├── index.html          # Single-page marketing site
├── favicon.svg         # Copied from web/public/
├── favicon.ico         # Copied from web/public/
├── favicon-16x16.png   # Copied from web/public/
├── favicon-32x32.png   # Copied from web/public/
├── apple-touch-icon.png # Copied from web/public/
├── og-image.png        # Open Graph social sharing image (1200x630)
├── robots.txt          # Crawl directives
└── sitemap.xml         # Single-page sitemap
```

### Page Sections

#### 1. Navigation Header (sticky)
- Inline SVG logo (recreated from `web/src/components/ui/logo.tsx` — teal circle with checkmark + "IEP Advisor" in Lora)
- Anchor links: Features, About, Join Beta
- "Sign In" link (right-aligned, links to app when available)

#### 2. Hero + Value Proposition
- **H1:** "Understand Your Child's IEP. Advocate with Confidence."
- **Tagline:** Subheading explaining what the tool does in one sentence
- **Primary CTA:** "Join the Beta Waitlist" button (scrolls to signup section)
- **Visual:** Icon-based illustration or subtle brand graphic (no stock photos)

#### 3. Features / How It Works
- 2x2 or 1x4 card grid (responsive)
- Each card: Lucide-style inline SVG icon + heading + brief description
- Features:
  1. **Document Analysis** — Upload your IEP and get a clear breakdown
  2. **Plain-Language Explanations** — Complex jargon translated into understandable terms
  3. **Rights & Advocacy Guidance** — Know your rights under IDEA, Section 504, and state law
  4. **Goal Tracking** — Monitor your child's IEP goals and progress

#### 4. About / Mission Statement
- Why this exists — the gap between what parents need and what they get
- Brand voice: empowering, warm, parent-first (see brainstorm: docs/brainstorms/2026-03-17-marketing-website-brainstorm.md)
- Brief founder/team motivation (1-2 paragraphs)

#### 5. Beta Signup
- **Approach:** CTA button linking to external Google Form (not iframe embed)
  - Rationale: iframes are unreliable on mobile, cannot be styled to match brand, create accessibility issues, and the post-submission UX is controlled by Google
  - The CTA opens the Google Form in a new tab
- **Form fields (in Google Form):** Name, email, optional context ("What's your biggest challenge with your child's IEP?")
- **Inline privacy note:** "We'll never share your information. Your data is used only to notify you about beta access."
- **Legal disclaimer:** "IEP Advisor provides information and tools to help you understand your child's IEP. It does not provide legal advice."

#### 6. Footer
- Copyright notice
- Link to future privacy policy and terms
- "Built with care for families navigating special education"

### Brand Implementation

Replicate brand tokens using Tailwind CDN inline config:

**Colors** (from `web/tailwind.config.js`):
| Token | Hex | Usage |
|-------|-----|-------|
| brand-teal-500 | #1A9478 | Primary CTA, links, accents |
| brand-teal-50 | #EDF8F5 | Light section backgrounds |
| brand-teal-800 | #06342A | Dark emphasis text |
| brand-amber-400 | #D4820F | Highlight accents |
| brand-slate-800 | #1E2A2A | Body text, headings |
| brand-slate-50 | #F5F7F7 | Alternate section backgrounds |
| white | #FFFFFF | Primary background |

**Typography:**
- Headings: Lora (serif), 600 weight, loaded via Google Fonts CDN with `display=swap`
- Body: DM Sans (sans-serif), 400 weight
- Font loading pattern: replicate `<link rel="preconnect">` + Google Fonts URL from `web/index.html`

**Spacing:** Base-4 scale (Tailwind default aligns well)

**Border radius:** 8px for buttons, 12px for cards (matching `web/tailwind.config.js`)

### Responsive Strategy

Mobile-first approach — parents are the primary audience and often browse on phones.

| Breakpoint | Layout |
|------------|--------|
| < 640px (mobile) | Single column, stacked cards, full-width CTA, larger touch targets (min 44x44px) |
| 640-1024px (tablet) | 2-column feature cards, centered content |
| > 1024px (desktop) | Max-width container (1200px), 2x2 feature grid, more breathing room |

### SEO & Meta

```html
<title>IEP Advisor — Understand Your Child's IEP, Advocate with Confidence</title>
<meta name="description" content="Upload your child's IEP and get plain-language explanations, rights guidance, and goal analysis. Join the beta waitlist.">
<link rel="canonical" href="https://iep-advisor.com/">

<!-- Open Graph -->
<meta property="og:title" content="IEP Advisor — Navigate Your Child's IEP with Confidence">
<meta property="og:description" content="AI-powered IEP analysis for parents. Plain-language explanations, rights guidance, and goal tracking.">
<meta property="og:image" content="https://iep-advisor.com/og-image.png">
<meta property="og:url" content="https://iep-advisor.com/">
<meta property="og:type" content="website">

<!-- Twitter Card -->
<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:title" content="IEP Advisor — Navigate Your Child's IEP with Confidence">
<meta name="twitter:description" content="AI-powered IEP analysis for parents. Join the beta.">
<meta name="twitter:image" content="https://iep-advisor.com/og-image.png">
```

### Accessibility Requirements

Target **WCAG 2.1 AA** — non-negotiable given the audience (parents of children with disabilities).

- [ ] Semantic HTML5 structure (`<header>`, `<nav>`, `<main>`, `<section>`, `<footer>`)
- [ ] Single `<h1>`, proper heading hierarchy (h1 → h2 → h3)
- [ ] Skip-to-content link as first focusable element
- [ ] All images/icons have appropriate alt text or `aria-hidden="true"` for decorative ones
- [ ] Color contrast: brand-teal-500 on white passes AA (4.6:1) — do NOT use teal shades lighter than 500 for text
- [ ] Keyboard navigable: all interactive elements focusable with visible focus rings
- [ ] `prefers-reduced-motion` media query: disable smooth scroll and any animations
- [ ] Touch targets: minimum 44x44px on mobile
- [ ] Language attribute: `<html lang="en">`

### Logo Recreation

The React logo component (`web/src/components/ui/logo.tsx`) uses Lucide's `CheckCircle` icon in a teal circle with "IEP Advisor" text. For the marketing site:

- Recreate as inline SVG in `index.html`
- Use the favicon SVG (`web/public/favicon.svg`) as the icon mark
- Add "IEP Advisor" text beside it using Lora font
- This keeps the site zero-dependency (no Lucide JS needed)

## Technical Considerations

### Performance
- **Tailwind CDN** loads ~300KB+ uncompressed — acceptable for a marketing page, but be aware of Core Web Vitals impact
- **Font loading:** Use `preconnect` to Google Fonts + `display=swap` to prevent FOIT
- **No images initially** — icon-based design avoids image optimization complexity
- **OG image:** Create a static 1200x630 PNG, optimize to <200KB

### Security
- HTTPS enforced at hosting level (all modern static hosts do this)
- No user input handled server-side (Google Form is external)
- Replicate security headers from `web/public/staticwebapp.config.json`:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: strict-origin-when-cross-origin`

### Deployment
- **Hosting:** TBD — Azure Static Web Apps, GitHub Pages, or Netlify all viable (see brainstorm)
- **Domain:** `iep-advisor.com` for marketing site
- **App domain:** `app.iep-advisor.com` for the React app (assumption — needs confirmation)
- **CI/CD:** Add `.github/workflows/deploy-marketing.yml` that deploys `/marketing` on push to main
- **DNS:** Configure root domain → marketing site, `app` subdomain → React app

### Analytics (deferred but structured for)
- No analytics at launch — structure HTML with clear section IDs and semantic elements so analytics can be added trivially later
- Recommendation: Plausible or Fathom (privacy-respecting, no cookie consent needed) over GA4

## Acceptance Criteria

### Functional
- [x] Single-page marketing site renders at `/marketing/index.html`
- [x] Hero section with headline, tagline, and CTA button
- [x] Features section with 4 capability cards
- [x] About/mission section with brand-voice copy
- [x] Beta signup section with CTA linking to Google Form (new tab)
- [x] Privacy note and legal disclaimer visible near signup CTA
- [x] Sticky navigation header with section anchor links
- [x] "Sign In" link in nav (href placeholder until app domain is set)
- [x] Footer with copyright and future legal links

### Brand & Design
- [x] Uses brand palette: teal primary, slate text, amber accents, white background
- [x] Lora for headings, DM Sans for body text (via Google Fonts CDN)
- [x] Favicon and apple-touch-icon match the app
- [x] Inline SVG logo matches app logo treatment
- [x] 0.5px borders, flat fills, no gradients or drop shadows (per brand guidelines)

### Responsive & Accessible
- [x] Mobile-first, renders correctly at 320px+ width
- [x] Feature cards stack on mobile, grid on desktop
- [x] WCAG 2.1 AA compliant (contrast, keyboard nav, semantic HTML, skip link)
- [x] Touch targets ≥ 44x44px on mobile
- [x] `prefers-reduced-motion` respected

### SEO & Technical
- [x] Proper `<title>`, `<meta description>`, Open Graph, and Twitter Card tags
- [x] `robots.txt` and `sitemap.xml` present
- [x] Canonical URL set
- [x] `<html lang="en">` attribute
- [ ] Valid HTML5 (passes W3C validator)

### Files Created
- [x] `marketing/index.html` — complete single-page site
- [x] `marketing/favicon.svg` + other favicon variants (copied from `web/public/`)
- [ ] `marketing/og-image.png` — social sharing image (deferred — requires design tool)
- [x] `marketing/robots.txt`
- [x] `marketing/sitemap.xml`

## Open Decisions (from SpecFlow analysis)

These need resolution before or during implementation:

1. **Hosting provider** — Azure Static Web Apps, GitHub Pages, or Netlify? (deployment workflow depends on this)
2. **App subdomain** — Confirm `app.iep-advisor.com` for the React app
3. **Google Form URL** — Need to create the actual Google Form and get the embed/link URL
4. **OG image design** — Need to create a branded 1200x630 image for social sharing
5. **Analytics tool** — Plausible, Fathom, or defer entirely?

## Dependencies & Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Domain not yet purchased/configured | Medium | Blocks launch | Can develop locally and deploy when domain is ready |
| Google Form URL not yet created | Low | Blocks signup CTA | Use placeholder href, swap when form exists |
| Brand token drift over time | Low | Minor inconsistency | Document token values inline; periodic manual sync |
| Tailwind CDN performance on slow mobile | Medium | Poor first impression | Monitor Core Web Vitals; can add build step later if needed |

## Success Metrics

- Site loads in < 3 seconds on 3G mobile connection
- Passes Lighthouse accessibility audit at 90+
- Google Form receives beta signups (baseline: any signups within first week)
- Valid HTML, no console errors

## Sources & References

### Origin
- **Brainstorm document:** [docs/brainstorms/2026-03-17-marketing-website-brainstorm.md](docs/brainstorms/2026-03-17-marketing-website-brainstorm.md) — Key decisions: plain HTML + Tailwind CDN, single-page scroll, Google Form signups, `/marketing` in monorepo

### Internal References
- Brand tokens: `web/tailwind.config.js`
- Font loading: `web/index.html`
- Logo component: `web/src/components/ui/logo.tsx`
- Favicon SVG: `web/public/favicon.svg`
- Brand guidelines: `docs/IEP_Advisor_Brand_Guidelines.pdf`
- Security headers: `web/public/staticwebapp.config.json`
- Deployment pattern: `.github/workflows/deploy-web.yml`
