# Design Discussion: Advocate page UI polish

**Date:** 2026-09-20
**Feature:** Fix the advocate page's unanchored composer, irregular margins and washed-out contrast — layout and visual only, no behaviour change.
**Screenshot under review:** parent view of `/children/:childId/advocate`, empty conversation, desktop ≈1340 px.

## Current State

- `web/src/features/advocate/components/advocate-page.tsx` — a `space-y-6` stack of: section heading + subtitle, `PrivacyBanner`, `UsageNotice`, then a two-column grid `md:grid-cols-[15rem_minmax(0,1fr)]`. The right column is a plain `<section className="space-y-3">` holding, in order, the empty state (or `MessageList`), error notices and the composer dock.
- The composer dock (`advocate-page.tsx:305`) is `sticky bottom-0 -mx-4 … bg-white … md:static md:mx-0 md:border-0 md:px-0`. On desktop it keeps `bg-white` and `pt-2` but loses its border, so it renders as a bare white band under a centred empty state — the "chat box hanging out there".
- `Composer` (`composer.tsx`) is a bare `<textarea>` plus a row holding the counter and the Send button; nothing groups them.
- `AdvocateEmptyState` wraps the shared `EmptyState` (`px-6 py-12`, centred). Its title ("Ask about Jacob's plan, your rights, or what to do next") and description restate the section heading immediately above it ("Ask the advocate about Jacob" / "Plain answers about the plan, your rights, and what to do next").
- `ThreadList` renders "New conversation" as a full-width secondary button, then rows padded `px-2`; the rail has no container, so its left edge and the conversation column's content edge do not line up.
- Palette in play: page `bg-brand-slate-50` (#F5F7F7) inside `max-w-7xl` (`main-layout.tsx:26,30`); `Card` uses `border-[0.5px] border-brand-slate-200`; most secondary copy is `text-brand-slate-400` (#7F9292) — the privacy banner, thread timestamps, empty-state description, composer counter and page subtitle are all at or below that weight.
- Nothing on the page sets a height, so on a tall viewport the whole feature occupies the top ~55 % and the composer floats mid-screen with dead space beneath it.

## Patterns to Follow

- `Card` (`components/ui/card.tsx`) — the project's panel treatment; `accent` adds a teal left rule.
- `MessageList` already owns its own scroll region (`max-h-[60vh] … overflow-y-auto`, `rounded-card bg-brand-slate-50`) — the panel should take over that job rather than nesting two scrollers.
- `Drawer`/`Menu`/`Notice`/`Button`/`EmptyState` stay as-is; this pass changes composition and tokens, not primitives.
- `docs/solutions/…/2026-09-17-rich-text-everywhere-…md` and the phone-dock work from the advocate plan: the composer must stay sticky and safe-area padded below `md`.

## Desired End State

**One conversation panel.** The right column becomes a single `Card`-styled panel that is a flex column with a real height (`min-h-[34rem]`, `md:h-[calc(100vh-19rem)]`): messages scroll in the middle, the composer is docked inside the panel behind a hairline divider. The composer is then visibly *part of* the conversation instead of a white band under it. Below `md` the panel loses its height and the composer returns to today's sticky-to-viewport dock.

**Composer as one input group.** Textarea, counter and Send live inside a single rounded, bordered group that takes the focus ring as a whole (`focus-within`). Send is a filled teal button; the counter only colours up when the limit is close or exceeded.

**Rail as a matching panel.** A card with a small "Conversations" label, an icon-led "New" button aligned to that label, and thread rows padded to the card's own padding so the rail's text baseline lines up with the conversation panel's.

**One heading, not two.** The section heading and subtitle stay; the empty state drops its restated title and becomes a short prompt above the example chips, vertically centred in the panel's free space.

**Contrast pass.** Secondary text moves from `slate-400` to `slate-500`; metadata (timestamps, counter) to `slate-500`; panel borders from `[0.5px]` to `1px` `slate-200`; the privacy line becomes a single quiet row with a `slate-500` lock rather than a filled `slate-50` box competing with the panel. Example chips gain weight (teal-600 text on white, teal-200 border, `text-sm`).

## Design Decisions

1. **Panel owns the scroll; `MessageList` gives it up.** Two nested scroll containers is the current shape and it is why the composer can drift far from the messages. `MessageList` keeps its `role="region"`/`tabIndex`/aria-label and loses `max-h-[60vh]`.
2. **Height is viewport-derived on desktop only** (`md:h-[calc(100vh-19rem)]`, floor `min-h-[34rem]`). Below `md` nothing is pinned — the existing sticky dock already solves phones, and a fixed height would fight the mobile keyboard.
3. **Composer group, not a bare textarea.** The whole group carries the border and focus ring; internal controls sit on the same white field. Keeps the current keyboard contract (Enter sends, Shift+Enter newline, read-only while streaming).
4. **Drop the empty state's duplicate title.** Replace with a one-line prompt ("Not sure where to start?") over the chips; keep the disclaimer sentence, which is the one line that is not redundant.
5. **No new dependency, no new primitive.** All of this is composition plus token swaps inside `features/advocate` — except one shared-primitive question (see Open Questions).
6. **Behaviour untouched.** No change to SSE, threads, citations, suggestions, usage or a11y contracts: the `role="status"` announcement node stays outside the (non-busy) region, the streaming bubble stays `aria-hidden`, Stop/Retry/cap states keep their testids.
7. **Phone layout re-verified, not redesigned.** The composer dock, Conversations drawer and safe-area padding stay; only the desktop panel is new.

## Resolved Questions (2026-09-20, after the mockup)

Mockup `docs/designs/2026-09-20-advocate-ui-polish-mockup.html` (v1, three views: empty, answered, phone). The annotation session was ended without queued feedback, so the loop closed on the text decisions below. You selected "keep defaults" **and** all three change options; since every change option contradicts the default, and your original note said the page looks washed out *in general*, I read that as "make all three changes" and sized the plan accordingly. Phase 3 is isolated so it can be dropped without touching the rest.

1. **Contrast scope: global.** `text-brand-slate-400` (#7F9292) measures **3.27:1 on white** and 3.04:1 on the `slate-50` page — under the 4.5:1 AA floor for body text — and appears **283 times across 154 files**. `slate-500` (#5A6F6F) is 5.33:1 / 4.96:1. So the sweep is a genuine a11y fix, applied app-wide to *text* uses; icon-only and placeholder uses stay (3:1 non-text bar).
2. **Rail: titles wrap.** Two-line clamp instead of truncation; rail stays 16 rem.
3. **User bubble: neutral.** Parent messages keep a neutral surface (`slate-100`, `slate-800` text) rather than the mockup's solid teal — the answer stays the page's only strong colour.

## Open Questions

None blocking. Carried into the plan as assumptions: the empty state keeps its icon (matches other empty states), and the panel height formula (`min-h-[34rem]`, `md:h-[calc(100vh-19rem)]`) is tuned during implementation against the real header/tab stack rather than fixed here.

## Testing Strategy

- vitest: existing advocate suites must pass unchanged (they assert testids, roles and the aria contract, not classes); add one case that the composer is inside the conversation panel and one that `MessageList` no longer sets its own max-height if that is asserted anywhere.
- `npm run lint` at the 36-error baseline, `tsc -b`, `npm run build`, `npm run guard:ux` (no raw spinners/reds/`confirm`).
- Manual: desktop 1440/1280, tablet 900, phone 400 — composer reachable, no double scrollbar, empty and populated threads, streaming state, cap state, viewer state.
