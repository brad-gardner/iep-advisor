import { useEffect, useRef } from 'react';
import { MessageSquare, X } from 'lucide-react';
import type { UseDocumentChatResult } from '../../hooks/use-document-chat';
import { ChatComposer } from './chat-composer';
import { ChatMessageBubble } from './chat-message-bubble';

interface ChatPanelProps {
  /** The thread, owned by the editor so it survives open/close. */
  chat: UseDocumentChatResult;
  /** Render the panel's own header + close control (omit when a Drawer
   *  already provides both). */
  onClose?: () => void;
}

// Document-scoped assistant thread: a side column on wide screens, hosted in
// a Drawer on narrow ones.
export function ChatPanel({ chat, onClose }: ChatPanelProps) {
  const { messages, isSending, error, send } = chat;
  const scrollRef = useRef<HTMLDivElement>(null);

  // Keep the latest message in view as the thread grows.
  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight });
  }, [messages, isSending]);

  return (
    <aside
      className="flex h-full flex-col rounded-card border border-brand-slate-200 bg-brand-slate-50"
      aria-label="Document assistant"
      data-testid="chat-panel"
    >
      {onClose && (
        <header className="flex items-center justify-between border-b border-brand-slate-200 px-4 py-3">
          <h2 className="flex items-center gap-2 text-sm font-medium text-brand-slate-700">
            <MessageSquare className="h-4 w-4 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
            Assistant
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="rounded-button p-1 text-brand-slate-400 hover:bg-brand-slate-100 hover:text-brand-slate-600"
            aria-label="Close assistant"
            data-testid="chat-close"
          >
            <X className="h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
          </button>
        </header>
      )}

      <div
        ref={scrollRef}
        role="log"
        aria-live="polite"
        className="flex-1 space-y-3 overflow-y-auto p-4"
        data-testid="chat-thread"
      >
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
