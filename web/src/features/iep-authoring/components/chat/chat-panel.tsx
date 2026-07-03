import { useEffect, useRef } from 'react';
import { MessageSquare, X } from 'lucide-react';
import { useIepChat } from '../../hooks/use-iep-chat';
import { ChatComposer } from './chat-composer';
import { ChatMessageBubble } from './chat-message-bubble';

interface ChatPanelProps {
  draftId: number;
  onClose: () => void;
}

// Collapsible sidebar that hosts the ephemeral, IEP-scoped chat thread.
export function ChatPanel({ draftId, onClose }: ChatPanelProps) {
  const { messages, isSending, error, send } = useIepChat(draftId);
  const scrollRef = useRef<HTMLDivElement>(null);

  // Keep the latest message in view as the thread grows.
  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight });
  }, [messages, isSending]);

  return (
    <aside
      className="flex h-full flex-col rounded-card border border-brand-slate-200 bg-brand-slate-50"
      aria-label="IEP assistant chat"
      data-testid="chat-panel"
    >
      <header className="flex items-center justify-between border-b border-brand-slate-200 px-4 py-3">
        <h2 className="flex items-center gap-2 text-sm font-medium text-brand-slate-700">
          <MessageSquare className="h-4 w-4 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
          IEP assistant
        </h2>
        <button
          type="button"
          onClick={onClose}
          className="rounded-button p-1 text-brand-slate-400 hover:bg-brand-slate-100 hover:text-brand-slate-600"
          aria-label="Close chat"
          data-testid="chat-close"
        >
          <X className="h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
        </button>
      </header>

      <div ref={scrollRef} className="flex-1 space-y-3 overflow-y-auto p-4" data-testid="chat-thread">
        {messages.length === 0 && (
          <p className="text-[13px] leading-relaxed text-brand-slate-400" data-testid="chat-empty">
            Ask about this IEP — e.g. &ldquo;Is this goal measurable?&rdquo;
          </p>
        )}
        {messages.map((message, index) => (
          <ChatMessageBubble key={index} message={message} index={index} />
        ))}
        {isSending && (
          <p className="text-[13px] text-brand-slate-400" data-testid="chat-thinking">
            Thinking…
          </p>
        )}
        {error && (
          <p className="text-[13px] text-brand-danger-700" data-testid="chat-error">
            {error}
          </p>
        )}
      </div>

      <ChatComposer disabled={isSending} onSend={send} />
    </aside>
  );
}
