import { Link } from 'react-router-dom';
import { BookOpen, ClipboardCopy, NotebookPen, Target } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Markdown } from '@/components/ui/markdown';
import { TRUNCATED_NOTICE_COPY } from '../lib/copy';
import type { AdvocateCitation, AdvocateSuggestion } from '../types/advocate';

export interface SuggestionHandlers {
  /** `prep_question` — interim handoff: copies the question for meeting prep. */
  onCopyPrepQuestion: (text: string) => void;
  /** `journal_entry` — opens the journal drawer prefilled. */
  onJournalEntry: (text: string, date: string | null) => void;
}

interface AssistantMessageProps {
  childId: number;
  contentMarkdown: string;
  citations?: AdvocateCitation[];
  suggestions?: AdvocateSuggestion[];
  truncated?: boolean;
  handlers: SuggestionHandlers;
  'data-testid'?: string;
}

/** Where a citation chip leads; `kb` entries have a page, other kinds (Phase 3) do not yet. */
function citationHref(citation: AdvocateCitation): string | null {
  return citation.kind === 'kb' ? `/knowledge-base/${citation.id}` : null;
}

function citationLabel(citation: AdvocateCitation): string {
  if (citation.label) return citation.label;
  if (citation.kind === 'kb') return `Knowledge base #${citation.id}`;
  if (citation.kind === 'child') return 'Child profile';
  return `${citation.kind} #${citation.id}`;
}

const chipClass =
  'inline-flex items-center gap-1 rounded-badge border border-brand-teal-100 bg-brand-teal-50 px-2 py-0.5 text-xs font-medium text-brand-teal-600 hover:bg-brand-teal-100 focus:outline-none focus:ring-1 focus:ring-brand-teal-400 focus:ring-offset-1';

/**
 * One answer: sanitised markdown, then the sources it drew on (trust rule:
 * every claim traceable), then suggested next steps that land in existing
 * flows. Nothing here writes to the API.
 */
export function AssistantMessage({
  childId,
  contentMarkdown,
  citations = [],
  suggestions = [],
  truncated = false,
  handlers,
  'data-testid': testId = 'advocate-assistant-message',
}: AssistantMessageProps) {
  const visibleSuggestions = suggestions.filter(isRenderable);
  return (
    <li className="flex justify-start" data-testid={testId}>
      <div className="max-w-[92%] space-y-3 rounded-card rounded-bl-sm border-[0.5px] border-brand-slate-200 bg-white px-4 py-3">
        <Markdown content={contentMarkdown} className="text-sm" data-testid={`${testId}-content`} />

        {truncated && (
          <p className="text-xs text-brand-amber-500" data-testid={`${testId}-truncated`}>
            {TRUNCATED_NOTICE_COPY}
          </p>
        )}

        {citations.length > 0 && (
          <div className="flex flex-wrap items-center gap-1.5" data-testid={`${testId}-sources`}>
            <span className="text-xs font-medium text-brand-slate-500">Sources:</span>
            {citations.map((c, i) => {
              const href = citationHref(c);
              const label = citationLabel(c);
              return href ? (
                <Link key={`${c.kind}-${c.id}-${i}`} to={href} className={chipClass} data-testid="advocate-source-link">
                  <BookOpen className="h-3 w-3" aria-hidden="true" />
                  {label}
                </Link>
              ) : (
                <Badge key={`${c.kind}-${c.id}-${i}`} variant="neutral" data-testid="advocate-source-chip">
                  {label}
                </Badge>
              );
            })}
          </div>
        )}

        {visibleSuggestions.length > 0 && (
          <ul className="grid gap-2 sm:grid-cols-2" aria-label="Suggested next steps" data-testid={`${testId}-suggestions`}>
            {visibleSuggestions.map((s, i) => (
              <li key={`${s.kind}-${i}`}>
                <SuggestionCard suggestion={s} childId={childId} handlers={handlers} />
              </li>
            ))}
          </ul>
        )}
      </div>
    </li>
  );
}

function isRenderable(s: AdvocateSuggestion): boolean {
  switch (s.kind) {
    case 'prep_question':
    case 'journal_entry':
      return Boolean(s.text && s.text.trim());
    case 'open_kb':
      return typeof s.id === 'number';
    case 'open_goal':
      return true;
    default:
      return false;
  }
}

interface SuggestionCardProps {
  suggestion: AdvocateSuggestion;
  childId: number;
  handlers: SuggestionHandlers;
}

const cardClass = 'flex h-full flex-col gap-2 rounded-card border border-brand-slate-200 bg-brand-slate-50 p-3';
const titleClass = 'flex items-center gap-1.5 text-xs font-medium text-brand-slate-600';
const textClass = 'text-sm text-brand-slate-800';

function SuggestionCard({ suggestion, childId, handlers }: SuggestionCardProps) {
  const text = suggestion.text?.trim() ?? '';

  switch (suggestion.kind) {
    case 'prep_question':
      return (
        <div className={cardClass} data-testid="advocate-suggestion-prep_question">
          <p className={titleClass}>
            <ClipboardCopy className="h-3.5 w-3.5" aria-hidden="true" />
            Question for the meeting
          </p>
          <p className={textClass}>{text}</p>
          <Button
            type="button"
            variant="secondary"
            size="sm"
            className="mt-auto self-start"
            onClick={() => handlers.onCopyPrepQuestion(text)}
          >
            Copy question
          </Button>
        </div>
      );
    case 'journal_entry':
      return (
        <div className={cardClass} data-testid="advocate-suggestion-journal_entry">
          <p className={titleClass}>
            <NotebookPen className="h-3.5 w-3.5" aria-hidden="true" />
            Note for the journal
          </p>
          <p className={textClass}>{text}</p>
          <Button
            type="button"
            variant="secondary"
            size="sm"
            className="mt-auto self-start"
            onClick={() => handlers.onJournalEntry(text, suggestion.date ?? null)}
          >
            Add to journal
          </Button>
        </div>
      );
    case 'open_kb':
      return (
        <div className={cardClass} data-testid="advocate-suggestion-open_kb">
          <p className={titleClass}>
            <BookOpen className="h-3.5 w-3.5" aria-hidden="true" />
            Read more
          </p>
          {text && <p className={textClass}>{text}</p>}
          <Link
            to={`/knowledge-base/${suggestion.id}`}
            className="mt-auto self-start text-sm font-medium text-brand-teal-500 underline hover:text-brand-teal-600"
          >
            Open the guide
          </Link>
        </div>
      );
    case 'open_goal':
      return (
        <div className={cardClass} data-testid="advocate-suggestion-open_goal">
          <p className={titleClass}>
            <Target className="h-3.5 w-3.5" aria-hidden="true" />
            Look at the goals
          </p>
          {text && <p className={textClass}>{text}</p>}
          <Link
            to={`/children/${childId}/goals`}
            className="mt-auto self-start text-sm font-medium text-brand-teal-500 underline hover:text-brand-teal-600"
          >
            See goals
          </Link>
        </div>
      );
    default:
      return null;
  }
}
