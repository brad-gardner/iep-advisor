import { MessageCircleQuestion } from 'lucide-react';
import { EXAMPLE_QUESTIONS, JOURNAL_EXAMPLE_QUESTION } from '../lib/copy';

interface AdvocateEmptyStateProps {
  /** Puts the example into the composer (it is not sent until the parent chooses to). */
  onPickExample: (question: string) => void;
  /** Hide the examples for a viewer who cannot send. */
  canAsk: boolean;
  /** Adds the journal example when there is a journal to draw on. */
  hasJournalEntries?: boolean;
}

/**
 * A blank conversation: what the advocate is for, plus three (or four)
 * questions to start from. Deliberately does not repeat a title — the
 * section heading above the conversation panel already says "Ask the
 * advocate about {childFirstName}" — so this renders the shared `EmptyState`
 * primitive's icon/description shape by hand rather than through that
 * component (which always renders its own `title` heading).
 */
export function AdvocateEmptyState({ onPickExample, canAsk, hasJournalEntries = false }: AdvocateEmptyStateProps) {
  const examples = hasJournalEntries ? [...EXAMPLE_QUESTIONS, JOURNAL_EXAMPLE_QUESTION] : EXAMPLE_QUESTIONS;
  return (
    <div className="flex max-w-md flex-col items-center px-6 text-center" data-testid="advocate-empty">
      <div className="mb-3 text-brand-teal-500">
        <MessageCircleQuestion className="h-8 w-8" strokeWidth={1.5} aria-hidden="true" />
      </div>
      <p className="text-sm text-brand-slate-600">
        Plain answers that point back to the rules and to what's actually in the documents.{' '}
        <span className="text-brand-slate-500">Not legal advice.</span>
      </p>
      {canAsk && (
        <>
          <p className="mb-2 mt-5 text-xs font-medium text-brand-slate-500">Not sure where to start?</p>
          <ul className="flex flex-wrap justify-center gap-2" aria-label="Example questions">
            {examples.map((q) => (
              <li key={q}>
                <button
                  type="button"
                  onClick={() => onPickExample(q)}
                  className="rounded-button border-[1.5px] border-brand-teal-200 bg-white px-3 py-1.5 text-sm font-medium text-brand-teal-600 transition-colors hover:bg-brand-teal-50 focus:outline-none focus:ring-1 focus:ring-brand-teal-400 focus:ring-offset-2"
                  data-testid="advocate-example"
                >
                  {q}
                </button>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
