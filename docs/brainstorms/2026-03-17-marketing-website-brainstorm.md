# Marketing Website Brainstorm

**Date:** 2026-03-17
**Status:** Draft

## What We're Building

A standalone marketing website for IEP Advisor — a 1-2 page static site that introduces the platform to parents of children with IEPs and special education advocates/professionals. The site will use the existing brand identity (teal/amber/slate palette, Lora headings, DM Sans body) and drive beta signups via an embedded Google Form.

### Target Audience

- **Primary:** Parents navigating the IEP process who need help understanding and advocating for their children
- **Secondary:** Special education advocates, attorneys, and professionals who may refer parents to the tool

### Content Sections

1. **Hero + Value Proposition** — Bold headline, tagline ("Navigate with confidence"), clear CTA to join the beta waitlist
2. **Features / How It Works** — 2-4 key capabilities (document upload & analysis, plain-language explanations, rights & advocacy guidance, goal tracking)
3. **About / Mission Statement** — Why this exists, the problem being solved, the team's motivation
4. **Beta Signup** — Embedded Google Form or prominent link to one, collecting name + email + optional context

## Why This Approach

### Separate static site (not in-app routes)

- **SEO-friendly** — pure HTML, no client-side rendering needed
- **Independent deployment** — can iterate on marketing without touching the app
- **Lightweight** — no React overhead for what is essentially a brochure page
- **Simple hosting** — can deploy to Azure Static Web Apps, GitHub Pages, Netlify, or anywhere

### Plain HTML + Tailwind CSS (via CDN)

- Zero build step, maximum simplicity
- Tailwind CDN allows using the same utility classes and custom brand tokens
- Easy for anyone to edit without knowing React
- Fast to build and ship

### Google Form for beta signups

- No backend changes, no DB migration, no new API endpoint
- Responses automatically land in a Google Sheet for easy tracking/export
- Can be swapped for an in-app endpoint later if needed
- Embed directly or link out — both work fine

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Hosting model | Separate static site | SEO, independent deploys, simplicity |
| Technology | Plain HTML + Tailwind CDN | No build step, fast to ship, easy to maintain |
| Beta signup | Google Form (embedded) | Zero backend work, data in Google Sheets |
| Number of pages | 1 page (single scroll) | Simpler, all content fits on one well-structured page |
| Branding | Match existing app tokens | Teal primary, slate neutrals, Lora/DM Sans fonts |
| Repo location | `/marketing` in monorepo | Keeps branding co-located, easy to maintain |
| Domain | iep-advisor.com | Root domain for marketing, app hosted separately |
| Content | Draft placeholders | Realistic copy to refine later — fastest path to shipping |
| Deployment | TBD | GitHub Pages, Azure SWA, or Netlify — all viable |

## Resolved Questions

1. **Where should this live in the repo?** `/marketing` folder in the monorepo alongside `/web` and `/api`
2. **Domain/URL strategy?** iep-advisor.com — marketing site at root domain, app deployment separate
3. **Content approach?** Draft realistic placeholder copy that can be refined later

## Open Questions

None — ready for planning.
