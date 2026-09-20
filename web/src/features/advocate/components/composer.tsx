import { useId, useState, type KeyboardEvent } from 'react';
import { Send, Square } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/cn';
import { ADVOCATE_MESSAGE_MAX_LENGTH } from '../types/advocate';

interface ComposerProps {
  /** Controlled text — the page owns it so the empty-state examples can prefill. */
  value: string;
  onChange: (value: string) => void;
  onSend: (text: string) => void;
  onStop: () => void;
  /** An answer is streaming: typing is paused and Send becomes Stop. */
  streaming: boolean;
  /**
   * A send is in flight before any stream can start (the first message on a
   * thread has to create it first). Like `streaming` this pauses typing and
   * disables Send, but — unlike `disabled` — uses `readOnly` so the textarea
   * never loses focus, and there is nothing yet to Stop.
   */
  creating?: boolean;
  /** No sends at all (usage cap reached, thread unavailable). */
  disabled?: boolean;
  disabledReason?: string;
  childFirstName: string;
}

/**
 * The message box. Enter sends, Shift+Enter adds a line; a 2000-character
 * counter mirrors the server limit and blocks over-length sends rather than
 * trimming. While an answer streams (or the first thread is being created)
 * the textarea is read-only — never `disabled`, which would blur it — and
 * the draft is kept; the action button aborts the stream when one exists.
 */
export function Composer({
  value,
  onChange,
  onSend,
  onStop,
  streaming,
  creating = false,
  disabled = false,
  disabledReason,
  childFirstName,
}: ComposerProps) {
  const id = useId();
  const counterId = `${id}-count`;
  const [composing, setComposing] = useState(false);

  const length = value.length;
  const overLimit = length > ADVOCATE_MESSAGE_MAX_LENGTH;
  const canSend = !disabled && !streaming && !creating && value.trim().length > 0 && !overLimit;

  const submit = () => {
    if (!canSend) return;
    onSend(value.trim());
  };

  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    // Enter alone sends; Shift+Enter (and Enter mid-IME-composition) inserts a newline.
    if (e.key !== 'Enter' || e.shiftKey || composing || e.nativeEvent.isComposing) return;
    e.preventDefault();
    submit();
  };

  return (
    <form
      className="space-y-2"
      onSubmit={(e) => {
        e.preventDefault();
        submit();
      }}
      data-testid="advocate-composer"
    >
      <label htmlFor={id} className="sr-only">
        Ask the advocate about {childFirstName}
      </label>
      <div
        className={cn(
          'rounded-card border border-brand-slate-200 bg-white transition-colors',
          'focus-within:border-brand-teal-400 focus-within:ring-[3px] focus-within:ring-brand-teal-50',
          overLimit && 'border-brand-danger-200',
        )}
      >
        <textarea
          id={id}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          onKeyDown={onKeyDown}
          onCompositionStart={() => setComposing(true)}
          onCompositionEnd={() => setComposing(false)}
          rows={3}
          readOnly={streaming || creating}
          disabled={disabled}
          aria-describedby={counterId}
          aria-invalid={overLimit || undefined}
          placeholder={
            disabled
              ? (disabledReason ?? 'Sending is paused.')
              : `Ask about ${childFirstName}'s plan, a document, or what to do next…`
          }
          className={cn(
            'w-full resize-y rounded-t-card bg-transparent px-3 py-2 text-sm text-brand-slate-800 placeholder:text-brand-slate-300',
            'focus:outline-none',
            'disabled:cursor-not-allowed disabled:bg-brand-slate-50 read-only:bg-brand-slate-50',
          )}
          data-testid="advocate-composer-input"
        />
        <div className="flex items-center justify-between gap-3 px-3 pb-2">
          <p
            id={counterId}
            className={cn('text-xs', overLimit ? 'text-brand-danger-700' : 'text-brand-slate-400')}
            data-testid="advocate-composer-count"
          >
            {length.toLocaleString()} / {ADVOCATE_MESSAGE_MAX_LENGTH.toLocaleString()}
            {overLimit && ' — too long'}
            <span className="sr-only">. Enter sends, Shift+Enter starts a new line.</span>
          </p>
          {streaming ? (
            <Button type="button" variant="secondary" size="sm" onClick={onStop} data-testid="advocate-stop">
              <Square className="mr-1 h-3.5 w-3.5" aria-hidden="true" />
              Stop
            </Button>
          ) : (
            <Button type="submit" size="sm" disabled={!canSend} data-testid="advocate-send">
              <Send className="mr-1 h-3.5 w-3.5" aria-hidden="true" />
              Send
            </Button>
          )}
        </div>
      </div>
    </form>
  );
}
