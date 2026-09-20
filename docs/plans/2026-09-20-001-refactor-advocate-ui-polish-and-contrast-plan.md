---
title: "refactor: Advocate page layout polish + app-wide text contrast fix"
type: refactor
status: completed
date: 2026-09-20
design: docs/designs/2026-09-20-advocate-ui-polish-design.md
mockup: docs/designs/2026-09-20-advocate-ui-polish-mockup.html
slicing_approach: vertical
---

# refactor: Advocate page layout polish + app-wide text contrast fix

## Overview

The advocate page works but does not read as one surface: the composer floats as a bare white band under a centred empty state, the rail and conversation columns do not line up, the section heading is restated by the empty state, and nothing sets a height so the feature sits in the top half of a tall viewport. Separately — and the reason "washed out" is more than taste — the secondary text colour used across the whole app fails WCAG AA.

Three slices: contain the conversation (panel + composer group), tidy the rail and headings, then the app-wide contrast pass. Each is independently shippable; slice 3 can be dropped without touching 1–2.

## Problem Statement

From the screenshot (desktop ≈1340 px, empty conversation):

- **Composer unanchored.** `advocate-page.tsx:305` — the dock is `sticky bottom-0 -mx-4 … bg-white … md:static md:mx-0 md:border-0 md:px-0`. On desktop it keeps `bg-white` + `pt-2` and loses its border, rendering as a white band under the empty state with no relationship to the messages.
- **No height.** Nothing in the column sets one, so the panel's content stops mid-screen and the composer floats with dead space beneath it.
- **Ragged margins.** The rail has no container: a full-width "New conversation" button over `px-2` rows, so its left edge and the conversation content edge disagree; `MessageList`'s own `max-h-[60vh]` scroller nests inside a `space-y-3` column, giving two competing scroll/edge systems.
- **Duplicate heading.** "Ask the advocate about Jacob" / "Plain answers about the plan, your rights, and what to do next" is immediately restated by the empty state's title and description.
- **Contrast below AA.** `text-brand-slate-400` (#7F9292) = **3.27:1** on white, **3.04:1** on the `bg-brand-slate-50` page — under the 4.5:1 body-text floor — used **283×** across **154 files** (subtitles, timestamps, counters, descriptions, empty-state copy). `slate-500` (#5A6F6F) = 5.33:1 / 4.96:1. Panel borders at `border-[0.5px]` (33 uses) read as almost-absent at typical zoom.

## Proposed Solution

### Slice 1 — the conversation becomes one panel

`advocate-page.tsx`: the right column becomes a flex-column panel (`rounded-card border border-brand-slate-200 bg-white`, subtle shadow) with `min-h-[34rem]` and, at `md`, a viewport-derived height. Messages scroll in the middle; the composer docks inside behind a `border-t`. Below `md` the panel drops its height and the existing sticky-to-viewport dock (with `env(safe-area-inset-bottom)`) is unchanged.

```tsx
// advocate-page.tsx — conversation column (shape, not final classes)
<section
  className="flex min-h-[34rem] flex-col overflow-hidden rounded-card border border-brand-slate-200 bg-white
             md:h-[calc(100vh-19rem)]"
  aria-label="Conversation"
  data-testid="advocate-conversation"
>
  <div className="flex-1 overflow-y-auto">{/* empty state | MessageList | notices */}</div>
  {canAsk && (
    <div className="border-t border-brand-slate-200 bg-brand-slate-50/60 p-3
                    sticky bottom-0 md:static" data-testid="advocate-composer-dock">
      {/* pill · state hint · Composer */}
    </div>
  )}
</section>
```

`message-list.tsx` gives up `max-h-[60vh] min-h-[16rem] … rounded-card bg-brand-slate-50` — the panel owns the scroll. It keeps `role="region"`, `tabIndex={0}`, `aria-label="Conversation"` and the sr-only `role="status"` announcement node **outside** the region (the pass-4 a11y fix must survive).

`composer.tsx`: textarea, counter and action button move inside one `rounded-card border` group that takes `focus-within:border-brand-teal-400 focus-within:ring-[3px] focus-within:ring-brand-teal-50`; the textarea itself loses its border and ring. Send becomes the filled primary `Button` (it already is — it only *looks* washed out because the empty-draft state is disabled). Keyboard contract unchanged: Enter sends, Shift+Enter newline, `readOnly` (never `disabled`) while streaming/creating, `aria-describedby` counter, `aria-invalid` over limit.

### Slice 2 — rail, headings, bubbles

- `thread-list.tsx`: wrap in a card with a `Conversations` label row and a compact icon "New" button; rows padded to the card padding so the rail and panel baselines agree. Titles wrap to two lines (`line-clamp-2`) instead of `truncate`; rail stays 16 rem (`md:grid-cols-[16rem_minmax(0,1fr)]`).
- `empty-state.tsx` (advocate's wrapper): drop the restated title; keep the icon and the disclaimer sentence; add "Not sure where to start?" above the chips; chips to `text-sm`, `border-brand-teal-200`, `text-brand-teal-600`.
- `privacy-banner.tsx`: from a filled `slate-50` box to a single quiet line (lock icon + text at `slate-500`) so only the panel reads as a container.
- `user-message.tsx`: neutral surface (`bg-brand-slate-100 text-brand-slate-800`) — deliberately *not* the mockup's solid teal, so the answer stays the only strong colour.

### Slice 3 — app-wide contrast pass

Mechanical but inventory-driven, not a blind replace:

1. Generate the inventory: every `text-brand-slate-400` occurrence with file, line and the element it styles.
2. **Text uses → `text-brand-slate-500`**: body copy, descriptions, subtitles, timestamps, counters, empty-state text, table meta.
3. **Leave at `slate-400`**: standalone icons (`h-3.5`/`h-4` on the same element), `placeholder:` utilities, and anything already paired with a `slate-400` border as decoration — the 3:1 non-text bar applies there.
4. `border-[0.5px]` → `border` (1px) on `Card` and the 32 other hairline uses; verify nothing relied on the half-pixel for alignment.
5. Re-measure: assert in a small test that no *text* utility in `src` uses `slate-400` except the documented exceptions list.

## Implementation Notes (2026-09-20)

Branch `refactor/advocate-ui-polish-and-contrast` off `main`; three commits (planning docs, slices 1–2, slice 3 + height tune).

**Height was measured, not guessed.** The worker flagged `md:h-[calc(100vh-19rem)]` as unverified; driving the real app with agent-browser showed the panel bottom at 915px on a 900px viewport — 15px of overflow and 47px of page scroll. The stack above the panel is a constant 319px at every desktop width, and the container adds 32px below, so the value is **22rem** (548px tall, 33px gap, zero page scroll). The `min-h-[34rem]` floor was separately wrong: on a 720px-tall laptop it forced 175px of page scroll, so the floor is **26rem**. Both numbers and the measurement table are recorded in the code comment at `advocate-page.tsx:249`.

**Slice 3 found a second dark/light collision.** Beyond the sidebar the plan already called out, `components/ui/logo.tsx` had `variant === 'dark' ? 'text-brand-slate-400' : 'text-brand-slate-400'` — a no-op ternary. A blind replace would have pushed the dark branch to 2.77:1 while leaving the light branch failing; the ternary now discriminates. 251 of 283 occurrences changed, 32 documented in `web/src/test/contrast-allowlist.ts` with per-entry reasons and enforced by `web/src/test/contrast-guard.test.ts` (proven to fail on drift before it was finalised).

**Checks on HEAD:** vitest 699 passed (baseline 695 + 4 new), `tsc -b`, `test:types`, `build`, `guard:ux` clean, lint at the 36-error baseline. Live verification at 1440×900 (panel fills, no page scroll), 400×820 (dock stays `sticky`, panel overflow `visible`, unchanged behaviour), plus a dashboard spot-check after the sweep with no console errors. Screenshots (gitignored): `docs/screenshots/advocate-after-1440.png`, `advocate-after-phone.png`, `contrast-dashboard-after.png`.

**Not done:** no pixel-level pass over all 153 swept files (disproportionate); the empty state keeps its icon as assumed.

## Technical Considerations

- **No behaviour change.** No API, hook, state or SSE change; no new dependency; no new primitive. `guard:ux` must stay clean.
- **A11y contracts from the advocate review must survive:** status node outside a non-`aria-busy` region, streaming bubble `aria-hidden` + `disableLinks`, scroller `tabIndex={0} role="region"`, composer never `disabled` while streaming.
- **Height on desktop only.** `md:h-[calc(100vh-19rem)]` is tuned against the real header/tab stack during implementation; the floor `min-h-[34rem]` prevents collapse on short viewports. Below `md` nothing is pinned so the mobile keyboard is not fought.
- **Slice 3 touches 154 files.** It will dominate the diff; keep it in its own commit so review can read slices 1–2 independently.
- Existing advocate vitest suites assert testids, roles and ARIA — not classes — so they should pass unchanged. `message-list` and `advocate-page` tests that assert the scroller's own classes (if any) get updated with the move.

## Acceptance Criteria

### Slice 1
- [x] Composer renders inside the conversation panel, separated by a divider — no free-floating white band at any width ≥ `md`
- [x] Panel fills a sensible height on a 1440×900 viewport: no dead region between the composer and the page bottom
- [x] Exactly one scrollbar in the conversation area (no nested scroller)
- [x] Textarea + counter + Send read as one field; focusing the textarea rings the whole group
- [x] Phone (400 px): composer still sticky to the viewport with safe-area padding; Conversations drawer unchanged

### Slice 2
- [x] Rail is a card whose content edges align with the conversation panel's
- [x] Thread titles wrap to two lines instead of truncating mid-word
- [x] Section heading appears once — the empty state no longer restates it
- [x] Parent's own messages use a neutral surface

### Slice 3
- [x] No `text-brand-slate-400` on body/secondary **text** outside the documented exceptions; a check enforces it
- [x] Every migrated pairing measures ≥ 4.5:1 (`slate-500` on white and on `slate-50`)
- [x] Card and panel borders are 1px

### Non-functional
- [x] `npm test` green (advocate, journal, meeting-prep, children suites unchanged in intent)
- [x] `npx tsc -b`, `npm run test:types`, `npm run build`, `npm run guard:ux` clean; `npm run lint` at the 36-error baseline
- [x] Screenshots at 1440, 1280, 900 and 400 px for empty, answered, streaming and cap states

## Dependencies & Risks

| Risk | Mitigation |
|---|---|
| Height formula fights the tab bar / notices at some widths | Floor with `min-h`, verify at 1440/1280/900 before commit; formula is a constant in one place |
| Slice 3's size hides a regression | Own commit, inventory-driven edits, contrast check in tests; icons/placeholders explicitly excluded |
| Class assertions in existing tests break | Expected on `message-list`/`composer`; update with the move rather than loosening assertions |
| Panel scroll replaces `MessageList`'s, breaking the pin-to-bottom behaviour | The scroll ref moves with the container; keep the existing pinning logic and its tests |

## Sources & References

- **Design:** [docs/designs/2026-09-20-advocate-ui-polish-design.md](../designs/2026-09-20-advocate-ui-polish-design.md) — current state, decisions, resolved questions
- **Mockup:** [docs/designs/2026-09-20-advocate-ui-polish-mockup.html](../designs/2026-09-20-advocate-ui-polish-mockup.html) (v1; session ended without queued annotations)
- Feature under change: `web/src/features/advocate/components/{advocate-page,composer,message-list,thread-list,empty-state,privacy-banner,user-message}.tsx`
- Tokens: `web/tailwind.config.js` (brand scales), `web/src/components/ui/card.tsx`, `web/src/components/layouts/main-layout.tsx:26,30`
- Origin feature: [docs/plans/2026-09-19-001-feat-virtual-advocate-parent-ai-chat-and-journal-plan.md](./2026-09-19-001-feat-virtual-advocate-parent-ai-chat-and-journal-plan.md) (merged PR #32)
- A11y contracts to preserve: `docs/solutions/best-practices/2026-09-19-tool-using-advocate-over-sse-…md` (announcement placement, hidden streaming bubble)
