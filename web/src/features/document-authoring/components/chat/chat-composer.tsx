import { useState } from 'react';
import { SendHorizonal } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface ChatComposerProps {
  disabled: boolean;
  onSend: (text: string) => void;
}

// Text input + Send. Enter sends; Shift+Enter inserts a newline.
export function ChatComposer({ disabled, onSend }: ChatComposerProps) {
  const [value, setValue] = useState('');

  const submit = () => {
    const text = value.trim();
    if (!text || disabled) return;
    onSend(text);
    setValue('');
  };

  return (
    <form
      className="flex items-end gap-2 border-t border-brand-slate-200 p-3"
      onSubmit={(e) => {
        e.preventDefault();
        submit();
      }}
    >
      <label htmlFor="chat-composer-input" className="sr-only">
        Ask about this IEP
      </label>
      <textarea
        id="chat-composer-input"
        rows={2}
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            submit();
          }
        }}
        placeholder="Ask about this IEP…"
        className="w-full resize-none rounded-input border border-brand-slate-200 px-3 py-2 text-sm text-brand-slate-800 transition-colors placeholder:text-brand-slate-300 focus:border-brand-teal-400 focus:outline-none focus:ring-[3px] focus:ring-brand-teal-50"
        data-testid="chat-input"
      />
      <Button
        type="submit"
        variant="primary"
        className="px-3 py-2"
        disabled={disabled || value.trim().length === 0}
        data-testid="chat-send"
        aria-label="Send message"
      >
        <SendHorizonal className="h-4 w-4" strokeWidth={1.8} aria-hidden="true" />
      </Button>
    </form>
  );
}
