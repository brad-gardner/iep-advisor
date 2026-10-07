import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { BookOpen, ClipboardCopy, ListPlus, NotebookPen, Target } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Markdown } from '@/components/ui/markdown';
import { GeneratedLanguageNotice } from '@/lib/i18n/generated-language-notice';
import { citationHref, citationLabel, openGoalHref } from '../lib/citation-links';
import { truncatedNoticeCopy } from '../lib/copy';
import type { AdvocateCitation, AdvocateSuggestion } from '../types/advocate';

export interface SuggestionHandlers {
  /** `prep_question` — hands the question to meeting prep (`?addQuestion=`). */
  onAddPrepQuestion: (text: string) => void;
  /** `prep_question` secondary action: copies the question. */
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
  /** The language this persisted answer was generated in; null/unknown = English — shown via `GeneratedLanguageNotice` when it differs from the viewer's. */
  generatedLanguage?: 'en' | 'es' | null;
  handlers: SuggestionHandlers;
  'data-testid'?: string;
}

const chipClass =
  'inline-flex items-center gap-1 rounded-badge border border-brand-teal-100 bg-brand-teal-50 px-2 py-0.5 text-xs font-medium text-brand-teal-600 hover:bg-brand-teal-100 focus:outline-none focus:ring-1 focus:ring-brand-teal-500 focus:ring-offset-1';

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
  generatedLanguage,
  handlers,
  'data-testid': testId = 'advocate-assistant-message',
}: AssistantMessageProps) {
  const { t } = useTranslation('advocate');
  const visibleSuggestions = suggestions.filter(isRenderable);
  return (
    <li className="flex justify-start" data-testid={testId}>
      <div className="max-w-[92%] space-y-3 rounded-card rounded-bl-sm border border-brand-slate-200 bg-white px-4 py-3">
        <span className="sr-only">{t('message.srPrefix')}</span>
        <GeneratedLanguageNotice generatedLanguage={generatedLanguage} />
        <Markdown content={contentMarkdown} className="text-sm" data-testid={`${testId}-content`} />

        {truncated && (
          <p className="text-xs text-brand-amber-500" data-testid={`${testId}-truncated`}>
            {truncatedNoticeCopy()}
          </p>
        )}

        {citations.length > 0 && (
          <div className="flex flex-wrap items-center gap-1.5" data-testid={`${testId}-sources`}>
            <span className="text-xs font-medium text-brand-slate-500">{t('message.sourcesLabel')}</span>
            {citations.map((c, i) => {
              const href = citationHref(c, childId);
              const label = citationLabel(c);
              return href ? (
                <Link
                  key={`${c.kind}-${c.id}-${i}`}
                  to={href}
                  className={chipClass}
                  data-testid="advocate-source-link"
                  data-kind={c.kind}
                >
                  <BookOpen className="h-3 w-3" aria-hidden="true" />
                  {label}
                </Link>
              ) : (
                <Badge key={`${c.kind}-${c.id}-${i}`} variant="neutral" data-testid="advocate-source-chip" data-kind={c.kind}>
                  {label}
                </Badge>
              );
            })}
          </div>
        )}

        {visibleSuggestions.length > 0 && (
          <ul className="grid gap-2 sm:grid-cols-2" aria-label={t('message.suggestionsAria')} data-testid={`${testId}-suggestions`}>
            {visibleSuggestions.map((s, i) => (
              <li key={`${s.kind}-${i}`}>
                <SuggestionCard suggestion={s} childId={childId} citations={citations} handlers={handlers} />
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
  /** The same answer's sources — an `open_goal` with an id can reuse the goal's deep link. */
  citations: AdvocateCitation[];
  handlers: SuggestionHandlers;
}

const cardClass = 'flex h-full flex-col gap-2 rounded-card border border-brand-slate-200 bg-brand-slate-50 p-3';
const titleClass = 'flex items-center gap-1.5 text-xs font-medium text-brand-slate-600';
const textClass = 'text-sm text-brand-slate-800';

function SuggestionCard({ suggestion, childId, citations, handlers }: SuggestionCardProps) {
  const { t } = useTranslation('advocate');
  const text = suggestion.text?.trim() ?? '';

  switch (suggestion.kind) {
    case 'prep_question':
      return (
        <div className={cardClass} data-testid="advocate-suggestion-prep_question">
          <p className={titleClass}>
            <ClipboardCopy className="h-3.5 w-3.5" aria-hidden="true" />
            {t('suggestion.prepQuestionTitle')}
          </p>
          <p className={textClass}>{text}</p>
          <div className="mt-auto flex flex-wrap gap-2">
            <Button type="button" variant="secondary" size="sm" onClick={() => handlers.onAddPrepQuestion(text)}>
              <ListPlus className="mr-1 h-3.5 w-3.5" aria-hidden="true" />
              {t('suggestion.addToPrep')}
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => handlers.onCopyPrepQuestion(text)}>
              {t('suggestion.copy')}
            </Button>
          </div>
        </div>
      );
    case 'journal_entry':
      return (
        <div className={cardClass} data-testid="advocate-suggestion-journal_entry">
          <p className={titleClass}>
            <NotebookPen className="h-3.5 w-3.5" aria-hidden="true" />
            {t('suggestion.journalTitle')}
          </p>
          <p className={textClass}>{text}</p>
          <Button
            type="button"
            variant="secondary"
            size="sm"
            className="mt-auto self-start"
            onClick={() => handlers.onJournalEntry(text, suggestion.date ?? null)}
          >
            {t('suggestion.addToJournal')}
          </Button>
        </div>
      );
    case 'open_kb':
      return (
        <div className={cardClass} data-testid="advocate-suggestion-open_kb">
          <p className={titleClass}>
            <BookOpen className="h-3.5 w-3.5" aria-hidden="true" />
            {t('suggestion.readMoreTitle')}
          </p>
          {text && <p className={textClass}>{text}</p>}
          <Link
            to={`/knowledge-base/${suggestion.id}`}
            className="mt-auto self-start text-sm font-medium text-brand-teal-500 underline hover:text-brand-teal-600"
          >
            {t('suggestion.openGuide')}
          </Link>
        </div>
      );
    case 'open_goal': {
      const href = openGoalHref(suggestion.id, citations, childId);
      const direct = href.includes('#goal-');
      return (
        <div className={cardClass} data-testid="advocate-suggestion-open_goal">
          <p className={titleClass}>
            <Target className="h-3.5 w-3.5" aria-hidden="true" />
            {t('suggestion.goalsTitle')}
          </p>
          {text && <p className={textClass}>{text}</p>}
          <Link to={href} className="mt-auto self-start text-sm font-medium text-brand-teal-500 underline hover:text-brand-teal-600">
            {direct ? t('suggestion.openGoal') : t('suggestion.seeGoals')}
          </Link>
        </div>
      );
    }
    default:
      return null;
  }
}
