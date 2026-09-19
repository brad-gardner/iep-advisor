import { MessageCircleQuestion } from 'lucide-react';
import { EmptyState } from '@/components/ui/empty-state';
import { EXAMPLE_QUESTIONS, JOURNAL_EXAMPLE_QUESTION } from '../lib/copy';

interface AdvocateEmptyStateProps {
  childFirstName: string;
  /** Puts the example into the composer (it is not sent until the parent chooses to). */
  onPickExample: (question: string) => void;
  /** Hide the examples for a viewer who cannot send. */
  canAsk: boolean;
  /** Adds the journal example when there is a journal to draw on. */
  hasJournalEntries?: boolean;
}

/** A blank conversation: what the advocate is for, plus three (or four) questions to start from. */
export function AdvocateEmptyState({ childFirstName, onPickExample, canAsk, hasJournalEntries = false }: AdvocateEmptyStateProps) {
  const examples = hasJournalEntries ? [...EXAMPLE_QUESTIONS, JOURNAL_EXAMPLE_QUESTION] : EXAMPLE_QUESTIONS;
  return (
    <EmptyState
      icon={MessageCircleQuestion}
      title={`Ask about ${childFirstName}'s plan, your rights, or what to do next`}
      description="Plain answers that point back to the rules and to what's actually in the documents. Not legal advice."
      data-testid="advocate-empty"
      action={
        canAsk ? (
          <ul className="flex flex-wrap justify-center gap-2" aria-label="Example questions">
            {examples.map((q) => (
              <li key={q}>
                <button
                  type="button"
                  onClick={() => onPickExample(q)}
                  className="rounded-button border-[1.5px] border-brand-teal-300 bg-white px-3 py-1.5 text-xs font-medium text-brand-teal-600 transition-colors hover:bg-brand-teal-50 focus:outline-none focus:ring-1 focus:ring-brand-teal-400 focus:ring-offset-2"
                  data-testid="advocate-example"
                >
                  {q}
                </button>
              </li>
            ))}
          </ul>
        ) : undefined
      }
    />
  );
}
