import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { ArrowDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Markdown } from '@/components/ui/markdown';
import { markdownToPlainText } from '../lib/plain-text';
import type { AdvocateAnnouncement, PendingMessage, StreamingAnswer } from '../hooks/use-advocate-thread';
import type { AdvocateMessageDto } from '../types/advocate';
import { AssistantMessage, type SuggestionHandlers } from './assistant-message';
import { ToolActivity } from './tool-activity';
import { UserMessage } from './user-message';

interface MessageListProps {
  childId: number;
  messages: AdvocateMessageDto[];
  pending: PendingMessage | null;
  streaming: StreamingAnswer | null;
  /** The latest answer completed this session — announced once via the
   *  sr-only status node below. */
  announcement: AdvocateAnnouncement | null;
  handlers: SuggestionHandlers;
}

/** How close to the bottom (px) the reader must be for new content to keep the view pinned. */
const PIN_THRESHOLD_PX = 48;

/**
 * The same test for the page below `md`, where it is the page — not the panel — that scrolls, but
 * with more slack: a `block: 'nearest'` scroll stops the moment the sentinel's reserve is satisfied,
 * which leaves the page short of its own maximum (44px measured at 400x820 — the container's bottom
 * padding and the panel border sit below the dock). A threshold as tight as the scroller's would
 * read that perfectly-pinned state as "the reader scrolled away" and stop following the stream.
 */
const PAGE_PIN_THRESHOLD_PX = 96;

/**
 * The conversation, oldest first, followed by the in-flight user message and
 * the streaming answer. Auto-scrolls to the newest content unless the reader
 * has scrolled up to re-read something.
 *
 * Accessibility contract: the streaming text bubble is `aria-hidden` (never
 * re-read token by token) and rendered without links; tool-activity rows keep
 * their own status role so progress is still announced; the finished answer is
 * announced exactly once from an sr-only `role="status"` node rendered as a
 * sibling *outside* the conversation region, which carries no `aria-busy` —
 * a busy ancestor makes some AT drop the announcement instead of replaying it.
 *
 * Scroll ownership: this component's own `role="region"` div stays the single
 * scroller (ref, `onScroll`, pin-to-bottom logic unchanged). From `md` up the
 * conversation panel (`advocate-page.tsx`) gives it a bounded height via
 * `flex-1`, so it just fills that. Below `md` the panel has no fixed height
 * (it is content-sized so the page — not the panel — scrolls), which would
 * leave `flex-1` with nothing to size against: the scroller's `scrollHeight`
 * would equal its `clientHeight` and pin-to-bottom would become a no-op. The
 * `max-h-[60vh]` (removed again at `md`) keeps it a real, bounded scroller at
 * every width. The panel does not add a second scrolling wrapper around this
 * component, so there is exactly one scroller in the conversation area.
 *
 * Below `md` the composer dock is `position: sticky` (see `advocate-page.tsx`)
 * and pins to the *viewport's* bottom edge, while this scroller's own bottom
 * edge sits wherever the content ends — the two do not coincide, so no
 * constant amount of reserved space inside the scroller can keep the newest
 * message clear of the dock. Instead the tail sentinel below is scrolled into
 * view with a bottom scroll-margin: `scrollIntoView` satisfies that margin
 * against every scrollable ancestor (this scroller *and* the document), which
 * is the frame the dock actually lives in, and `block: 'nearest'` makes it a
 * no-op when the end is already visible. The margin is the dock's *measured*
 * height, published as `--advocate-dock-h` by `advocate-page.tsx`, because the
 * dock grows with the About pill, the state hint and the notice row; the
 * `10rem` in the class is only the fallback for environments without
 * ResizeObserver.
 *
 * Crossing those ancestors is why the page needs a pin test of its own. The
 * `onScroll` below watches this region only, and below `md` a reader can scroll
 * the *page* away without this region moving at all — so its scroll handler
 * never fires, `pinnedRef` stays true, and every streamed token would drag the
 * page back down (measured: page 386 -> reader scrolls to 0 -> next delta
 * snaps it to 386 again). `pagePinnedRef` closes that: the page follows the
 * stream only while the reader has left it near the bottom, and sending re-pins
 * it because the reader asked for that.
 */
export function MessageList({ childId, messages, pending, streaming, announcement, handlers }: MessageListProps) {
  const scrollerRef = useRef<HTMLDivElement>(null);
  const tailRef = useRef<HTMLDivElement>(null);
  const announcementText = useMemo(() => (announcement ? markdownToPlainText(announcement.text) : ''), [announcement]);
  const pinnedRef = useRef(true);
  const pagePinnedRef = useRef(true);
  const seenCountRef = useRef(messages.length);
  const [unseenAnswer, setUnseenAnswer] = useState(false);

  const onScroll = () => {
    const el = scrollerRef.current;
    if (!el) return;
    pinnedRef.current = el.scrollHeight - el.scrollTop - el.clientHeight <= PIN_THRESHOLD_PX;
  };

  useEffect(() => {
    const measurePage = () => {
      const max = document.documentElement.scrollHeight - window.innerHeight;
      // A page that cannot scroll (every width from `md` up, where the panel is height-bound) is
      // always "at the bottom" — the guard must not disable following there.
      pagePinnedRef.current = max <= 0 || max - window.scrollY <= PAGE_PIN_THRESHOLD_PX;
    };

    // `resize` as well as `scroll`: the page can stop being at its bottom (or start being at it)
    // with no scroll event at all — a rotation, the virtual keyboard, or the notice row mounting
    // and changing the document's height. Without it a stale `false` would silently stop the
    // stream from following until the reader happened to scroll again.
    window.addEventListener('scroll', measurePage, { passive: true });
    window.addEventListener('resize', measurePage, { passive: true });
    window.visualViewport?.addEventListener('resize', measurePage);
    return () => {
      window.removeEventListener('scroll', measurePage);
      window.removeEventListener('resize', measurePage);
      window.visualViewport?.removeEventListener('resize', measurePage);
    };
  }, []);

  // A new message always pins the conversation itself (the reader asked for it); streaming text
  // only follows while already pinned.
  useLayoutEffect(() => {
    pinnedRef.current = true;
  }, [messages.length, pending]);

  /**
   * The page is re-pinned when the reader *sends*, and deliberately not when an answer lands.
   * Settling an answer also changes `messages.length`, so folding the page into the rule above
   * would move the whole page — a thousand pixels, page header included — at the one moment a
   * reader who scrolled away to re-read something is least expecting it. On `md` and up the page
   * cannot scroll at all, so `pagePinnedRef` is always true there and completion still re-pins
   * exactly as it did before; this only changes the phone case the sticky dock introduced.
   */
  useLayoutEffect(() => {
    if (pending) pagePinnedRef.current = true;
  }, [pending]);

  const showLatest = () => {
    pinnedRef.current = true;
    pagePinnedRef.current = true;
    setUnseenAnswer(false);
    tailRef.current?.scrollIntoView({ block: 'nearest' });
  };

  useEffect(() => {
    if (pinnedRef.current && pagePinnedRef.current) {
      tailRef.current?.scrollIntoView({ block: 'nearest' });
      setUnseenAnswer(false);
    } else if (messages.length > seenCountRef.current) {
      // An answer landed somewhere the reader is not looking. The rule above deliberately does not
      // move them; the offer below is what replaces the jump, so completion is not left announced
      // only to assistive tech.
      setUnseenAnswer(true);
    }
    seenCountRef.current = messages.length;
  }, [messages, pending, streaming]);

  return (
    <>
      <div
        ref={scrollerRef}
        onScroll={onScroll}
        tabIndex={0}
        role="region"
        aria-label="Conversation"
        // `relative` is load-bearing, not decoration: the sr-only speaker labels inside the bubbles
        // ("You said: ", "Advocate: ") are `position: absolute`, and an absolutely-positioned box is
        // only clipped by an ancestor's `overflow` when that ancestor is also its containing block.
        // Without a positioned scroller they were laid out against the initial containing block
        // instead — measured at 400x820 with four messages, they landed ~857px below the content and
        // dragged `documentElement.scrollHeight` from 1250 to 2107, giving the phone that much dead
        // scroll below the page and breaking the page-bottom test the pin guard above depends on.
        className="relative min-h-0 max-h-[60vh] flex-1 overflow-y-auto p-3 md:max-h-none"
        data-testid="advocate-messages"
      >
        <ul className="space-y-3">
          {messages.map((m) =>
            m.role === 'User' ? (
              <UserMessage key={m.id} text={m.contentMarkdown} />
            ) : (
              <AssistantMessage
                key={m.id}
                childId={childId}
                contentMarkdown={m.contentMarkdown}
                citations={m.citations}
                suggestions={m.suggestions}
                truncated={m.truncated}
                handlers={handlers}
              />
            ),
          )}
          {pending && <UserMessage text={pending.text} pending data-testid="advocate-user-message-pending" />}
        </ul>

        {streaming && (
          <div className="mt-3 space-y-2" data-testid="advocate-streaming">
            {/* Tool rows keep their own status semantics so progress is still announced. */}
            <ToolActivity tools={streaming.tools} />
            {streaming.text ? (
              // The token stream is visual only: hidden from AT so it is not re-read per delta, and
              // rendered without links so nothing focusable sits inside the hidden subtree. The
              // finished answer is announced once from the status node below.
              <div className="flex justify-start" aria-hidden="true">
                <div className="max-w-[92%] rounded-card rounded-bl-sm border border-brand-slate-200 bg-white px-4 py-3">
                  <Markdown content={streaming.text} className="text-sm" disableLinks data-testid="advocate-streaming-text" />
                </div>
              </div>
            ) : (
              streaming.tools.length === 0 && (
                <p className="text-xs text-brand-slate-500" data-testid="advocate-thinking">
                  The advocate is thinking…
                </p>
              )
            )}
          </div>
        )}

        {/*
          Scroll target for pin-to-bottom. Zero height, so it never inflates `scrollHeight` (a spacer
          would make a short thread open pre-scrolled); the bottom scroll-margin is what keeps the
          newest message clear of the sticky dock on phones, and it is dropped at `md` where the dock
          sits in normal flow inside the panel.
        */}
        <div
          ref={tailRef}
          aria-hidden="true"
          className="scroll-mb-[var(--advocate-dock-h,10rem)] md:scroll-mb-0"
          data-testid="advocate-scroll-tail"
        />
      </div>

      {/*
        Only reachable below `md`: from `md` up the page cannot scroll, so `pagePinnedRef` is always
        true and a new message always scrolls, leaving nothing unseen. It rides the panel's sticky
        bottom edge, offset by the measured dock height so it sits just above the composer rather
        than behind it.
      */}
      {unseenAnswer && (
        <div className="pointer-events-none sticky bottom-[var(--advocate-dock-h,10rem)] z-10 flex justify-center">
          <Button
            type="button"
            variant="secondary"
            size="sm"
            className="pointer-events-auto shadow-sm"
            onClick={showLatest}
            data-testid="advocate-jump-latest"
          >
            <ArrowDown className="mr-1 h-3.5 w-3.5" aria-hidden="true" />
            New answer
          </Button>
        </div>
      )}

      {/* Outside the conversation region on purpose: a status node under an aria-busy ancestor is
          dropped by some AT (Gecko/NVDA) rather than replayed, so the region carries no aria-busy and
          the node lives beside it. Plain text, not markdown, so punctuation is not read aloud. */}
      <p role="status" className="sr-only" data-testid="advocate-announcement">
        {announcementText}
      </p>
    </>
  );
}
