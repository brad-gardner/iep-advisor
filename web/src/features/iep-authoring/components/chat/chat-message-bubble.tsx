import type { ChatMessage } from '../../api/iep-assist-types';

// A single user/assistant message bubble.
export function ChatMessageBubble({ message, index }: { message: ChatMessage; index: number }) {
  const isUser = message.role === 'user';
  return (
    <div
      className={`flex ${isUser ? 'justify-end' : 'justify-start'}`}
      data-testid={`chat-message-${index}`}
      data-role={message.role}
    >
      <div
        className={`max-w-[85%] whitespace-pre-wrap rounded-card px-3 py-2 text-[13px] leading-relaxed ${
          isUser
            ? 'bg-brand-teal-500 text-white'
            : 'border border-brand-slate-200 bg-white text-brand-slate-700'
        }`}
      >
        {message.content}
      </div>
    </div>
  );
}
