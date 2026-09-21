---
module: Advocate
date: "2026-09-20"
problem_type: ui_bug
component: react_component
symptoms:
  - "Newest chat message sits behind the sticky composer on phones after auto-scroll"
  - "Every streamed token drags the reader's page scroll back down"
  - "Short thread opens already scrolled, with a blank band under the last message"
  - "Phone page scrolls ~857px past the end of its own content"
root_cause: scope_issue
resolution_type: code_fix
severity: medium
tags: [pin-to-bottom, position-sticky, scrollintoview, scroll-margin, containing-block, resizeobserver, chat-ui]
---

# Pin-to-bottom when a sticky dock and a nested scroller disagree about which box is "the bottom"

## Problem

The Advocate is a streaming chat. Below `md` its layout is:

- the **page** scrolls (the conversation panel is content-sized there),
- the **message list** is its own scroller, capped at `max-h-[60vh]`,
- the **composer dock** is `position: sticky; bottom: 0`, so it floats over whatever the page
  renders behind it.

Auto-scroll ("pin to bottom") was `el.scrollTop = el.scrollHeight` on the message scroller. That is
correct on desktop, where the panel is height-bound and the dock sits in normal flow inside it. On a
phone it put the newest message underneath the floating dock, because the scroller's bottom edge and
the dock are in **different coordinate frames**: the scroller's bottom is wherever its content ends,
and the dock's bottom is the viewport's.

Three fixes were tried before one held, and the first two are the instructive part.

## What did not work

**1. A fixed spacer at the end of the scroller** (`<div className="h-40 md:hidden" />`).

Wrong frame, so no constant can be right. Measured at 400×820: the dock is 137px with just the
composer and up to ~261px with the About pill and the state hint showing, so 160px over-reserved in
the common case (55–184px of dead space) and still left 69px of the answer covered in the tallest
one. It also inflated `scrollHeight`, so a short thread opened already scrolled, and it skewed the
"is the reader at the bottom?" threshold by its own height.

**2. Keeping the spacer but measuring the dock.** Better, but still reserving space inside the wrong
box. The reserve has to be satisfied against the frame the dock actually lives in — the viewport.

## What worked

**A zero-height sentinel plus a scroll-margin, scrolled with `scrollIntoView`.**

```tsx
// last child of the scroller
<div ref={tailRef} aria-hidden="true" className="scroll-mb-[var(--advocate-dock-h,10rem)] md:scroll-mb-0" />

// the pin
if (pinnedRef.current && pagePinnedRef.current) tailRef.current?.scrollIntoView({ block: 'nearest' });
```

`scrollIntoView` satisfies the element's **scroll-margin box against every scrollable ancestor** —
the message scroller *and* the document. That is exactly the property needed: the dock lives in the
document's frame, so the document is the scroller that has to move. `block: 'nearest'` makes it a
no-op once the end is visible, and zero height means it never inflates `scrollHeight`, so short
threads open unscrolled.

The margin is **measured, not guessed**. A `ResizeObserver` on the dock *and* on the notice row above
it (stopped / Retry / disclaimer — covered by the same sticky dock) publishes their summed height as
a custom property on the panel:

```tsx
const measure = () => {
  const height = (noticesRef.current?.offsetHeight ?? 0) + (dockRef.current?.offsetHeight ?? 0);
  panel.style.setProperty('--advocate-dock-h', `${height}px`);
};
```

It fires only when one of those boxes resizes, never per streamed token, and the class keeps a
`10rem` fallback for environments without `ResizeObserver` (jsdom).

## The two bugs the fix itself introduced

**Crossing ancestors means the page is now yours to respect.** The "is the reader pinned?" test was
an `onScroll` handler on the message scroller. When the reader scrolls the *page*, that region does
not move, so its handler never fires and the ref stays `true` — and the next token's `scrollIntoView`
drags them back. Reproduced at 400×820: page 386 → reader scrolls to 0 → next delta 386 again, once
per token.

The fix is a second pin signal for the page, on `window`'s `scroll` **and** `resize` (plus
`visualViewport` resize — a rotation or the keyboard changes whether the page is at its bottom with
no scroll event at all). Its threshold has to be looser than the scroller's: a `block: 'nearest'`
scroll stops as soon as the reserve is satisfied, which measured 44px short of the page maximum, so a
48px test would read a perfectly-pinned page as "scrolled away".

Re-pinning the two surfaces also needs different rules. `messages.length` changes **twice** per turn
— once on send, once when the answer settles — so folding the page into the pre-existing "a new
message always pins" rule moved the whole page at the moment a reader who had scrolled away was least
expecting it. The region keeps that rule; the page re-pins on the *send* only:

```tsx
useLayoutEffect(() => { pinnedRef.current = true; }, [messages.length, pending]);
useLayoutEffect(() => { if (pending) pagePinnedRef.current = true; }, [pending]);
```

A corollary: if a retry hands `start()` the same `pending` object already in state, `setPending`
bails on reference equality, `pending`'s identity never changes, and **every effect keyed on it is
skipped** — so a retry silently stopped re-pinning. `start({ ...pending })` fixes it. Any effect that
treats a prop as an event needs that prop to actually change identity.

**An absolutely-positioned child escaped the scroller.** Found while measuring the page's height for
the new threshold: `documentElement.scrollHeight` was 2107 against a `body` of 1250 — 857px of dead
phone scroll. The cause was the sr-only speaker labels (`"You said: "`, `"Advocate: "`) inside the
message bubbles. Tailwind's `sr-only` is `position: absolute`, and **an absolutely-positioned box is
only clipped by an ancestor's `overflow` when that ancestor is also its containing block**. The
scroller was unpositioned, so they laid out against the initial containing block instead and landed
far below the content. `relative` on the scroller contains them, with no other effect — verified by
grepping the subtree for other positioned descendants.

## Verification

Driven in a real browser (agent-browser, authenticated, live SSE streams) at 400×820 and 1440×900:

| Check | Result |
|---|---|
| `--advocate-dock-h` with a disclaimer row showing | 241px (89 notices + 152 dock) |
| Newest message vs dock top, after pin | 89px clear, disclaimer fully visible, no dead band |
| Page scroll after pin | 386 of a 430 maximum |
| Reader scrolls page to 0 mid-stream | stays at 0 for the full ~12s stream *and* through completion |
| Reader at the page bottom | holds at 430, region gap 0 throughout |
| `documentElement.scrollHeight` vs `body.scrollHeight` | 1250 = 1250 (was 2107) |
| 1440×900 | sentinel `scroll-margin-bottom` 0px, dock `static`, no page scroll |
| 1440×720 / 1440×671 | page scrolls 47px / 96px — inside the threshold, so behaviour is unchanged |

Suite: vitest 703, `tsc -b`, `test:types`, `build`, `guard:ux` clean; lint at the repo's 36-error
baseline. Each new guard was mutation-checked — reverting the pin to the `scrollTop` write, removing
the page guard, dropping `relative`, and passing the shared `pending` reference each fail the test
that names them.

## Limitations

The `visualViewport` resize handler is not debounced, so a mid-animation keyboard geometry read can
flip the page-pin ref for one batch of tokens. It is self-correcting — the settled resize or the next
real scroll recomputes — and was left as a known P3 rather than fixed blind, since it needs a real
device to judge.

## Prevention

- When a sticky element and a scroller are in different frames, reserve space with **`scroll-margin`
  on a scroll target**, not with a spacer inside either box. `scrollIntoView` is the only API that
  resolves the reserve against every ancestor at once.
- Measure a floating element's height with a `ResizeObserver` and publish it as a custom property.
  Every hard-coded height in this feature was wrong for at least one real state.
- One "is the reader pinned?" signal **per scroll container**. A handler on the inner scroller says
  nothing about the page.
- `sr-only` is `position: absolute`. Any scroller holding sr-only content needs `relative`, or the
  content escapes and inflates the document.
- Check `documentElement.scrollHeight` against `body.scrollHeight` when a page scrolls further than
  its content. They should match.

## Related

- Origin feature: [docs/plans/2026-09-19-001-feat-virtual-advocate-parent-ai-chat-and-journal-plan.md](../../plans/2026-09-19-001-feat-virtual-advocate-parent-ai-chat-and-journal-plan.md)
- This change: [docs/plans/2026-09-20-001-refactor-advocate-ui-polish-and-contrast-plan.md](../../plans/2026-09-20-001-refactor-advocate-ui-polish-and-contrast-plan.md)
- A11y contracts this had to preserve: [2026-09-19-tool-using-advocate-over-sse-what-a-streaming-agent-in-aspnet-has-to-get-right.md](../best-practices/2026-09-19-tool-using-advocate-over-sse-what-a-streaming-agent-in-aspnet-has-to-get-right.md)
- Code: `web/src/features/advocate/components/message-list.tsx`, `advocate-page.tsx`,
  `web/src/features/advocate/hooks/use-advocate-thread.ts`
