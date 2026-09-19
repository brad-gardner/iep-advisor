import { useEffect, useLayoutEffect, useRef } from 'react';
import { Markdown } from '@/components/ui/markdown';
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
 * The streaming bubble is hidden from assistive tech (`aria-hidden`) so a
 * screen reader never re-reads it token by token; a separate sr-only
 * `role="status"` node announces the finished answer exactly once, after
 * `done` lands. `aria-busy` lives on the scroll region instead, so it still
 * tells assistive tech the conversation is updating while streaming.
 */
export function MessageList({ childId, messages, pending, streaming, announcement, handlers }: MessageListProps) {
  const scrollerRef = useRef<HTMLDivElement>(null);
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
    <div
      ref={scrollerRef}
      onScroll={onScroll}
      tabIndex={0}
      role="region"
      aria-label="Conversation"
      aria-busy={streaming !== null}
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

      <div aria-hidden="true" className="mt-3 space-y-2" data-testid="advocate-streaming">
        {streaming && (
          <>
            <ToolActivity tools={streaming.tools} />
            {streaming.text ? (
              <div className="flex justify-start">
                <div className="max-w-[92%] rounded-card rounded-bl-sm border-[0.5px] border-brand-slate-200 bg-white px-4 py-3">
                  <Markdown content={streaming.text} className="text-sm" data-testid="advocate-streaming-text" />
                </div>
              </div>
            ) : (
              streaming.tools.length === 0 && (
                <p className="text-xs text-brand-slate-400" data-testid="advocate-thinking">
                  The advocate is thinking…
                </p>
              )
            )}
          </>
        )}
      </div>

      <p role="status" className="sr-only" data-testid="advocate-announcement">
        {announcement?.text ?? ''}
      </p>
    </div>
  );
}
