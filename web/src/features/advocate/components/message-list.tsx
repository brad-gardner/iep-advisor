import { useEffect, useLayoutEffect, useMemo, useRef } from 'react';
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
 */
export function MessageList({ childId, messages, pending, streaming, announcement, handlers }: MessageListProps) {
  const scrollerRef = useRef<HTMLDivElement>(null);
  const announcementText = useMemo(() => (announcement ? markdownToPlainText(announcement.text) : ''), [announcement]);
  const pinnedRef = useRef(true);

  const onScroll = () => {
    const el = scrollerRef.current;
    if (!el) return;
    pinnedRef.current = el.scrollHeight - el.scrollTop - el.clientHeight <= PIN_THRESHOLD_PX;
  };

  // A new message always pins (the reader asked for it); streaming text only
  // follows while already pinned.
  useLayoutEffect(() => {
    pinnedRef.current = true;
  }, [messages.length, pending]);

  useEffect(() => {
    const el = scrollerRef.current;
    if (el && pinnedRef.current) el.scrollTop = el.scrollHeight;
  }, [messages, pending, streaming]);

  return (
    <>
      <div
        ref={scrollerRef}
        onScroll={onScroll}
        tabIndex={0}
        role="region"
        aria-label="Conversation"
        className="max-h-[60vh] min-h-[16rem] overflow-y-auto rounded-card bg-brand-slate-50 p-3 md:max-h-[calc(100vh-22rem)]"
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
                <div className="max-w-[92%] rounded-card rounded-bl-sm border-[0.5px] border-brand-slate-200 bg-white px-4 py-3">
                  <Markdown content={streaming.text} className="text-sm" disableLinks data-testid="advocate-streaming-text" />
                </div>
              </div>
            ) : (
              streaming.tools.length === 0 && (
                <p className="text-xs text-brand-slate-400" data-testid="advocate-thinking">
                  The advocate is thinking…
                </p>
              )
            )}
          </div>
        )}
      </div>

      {/* Outside the conversation region on purpose: a status node under an aria-busy ancestor is
          dropped by some AT (Gecko/NVDA) rather than replayed, and the region is busy at the exact
          commit the announcement lands. Plain text, not markdown, so punctuation is not read aloud. */}
      <p role="status" className="sr-only" data-testid="advocate-announcement">
        {announcementText}
      </p>
    </>
  );
}
