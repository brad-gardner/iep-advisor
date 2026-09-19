import { Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Markdown } from '@/components/ui/markdown';
import { AskAdvocateButton } from '@/features/advocate/components/ask-advocate-button';
import { cn } from '@/lib/cn';
import { formatDate } from '@/lib/format-date';
import { journalEntryElementId } from '../lib/entry-anchor';
import type { JournalEntryDto } from '../types/journal';
import { JournalTagBadge } from './journal-tag-badge';

interface JournalEntryItemProps {
  entry: JournalEntryDto;
  /** Present only when the viewer may edit — opens the entry in the drawer. */
  onEdit?: (entry: JournalEntryDto) => void;
  /** Shows the "Ask the advocate" launcher for this entry (parents who can ask). */
  canAsk?: boolean;
  /** The entry a deep link (`?entry=`) points at. */
  highlighted?: boolean;
}

/** One dated update: day, tag chip, the note as rendered markdown, and edit / ask-the-advocate affordances. */
export function JournalEntryItem({ entry, onEdit, canAsk = false, highlighted = false }: JournalEntryItemProps) {
  const day = formatDate(entry.occurredOn);
  return (
    <li
      id={journalEntryElementId(entry.id)}
      className={cn(
        'flex items-start justify-between gap-3 py-3 scroll-mt-20',
        highlighted && '-mx-2 rounded-card bg-brand-teal-50 px-2 ring-2 ring-brand-teal-300',
      )}
      data-testid={`journal-entry-${entry.id}`}
      data-highlighted={highlighted ? 'true' : undefined}
    >
      <div className="min-w-0 flex-1">
        <div className="mb-1 flex flex-wrap items-center gap-2">
          <time dateTime={entry.occurredOn} className="text-[11px] font-semibold uppercase tracking-wide text-brand-slate-500">
            {day}
          </time>
          <JournalTagBadge tag={entry.tag} data-testid={`journal-entry-${entry.id}-tag`} />
        </div>
        <Markdown content={entry.contentMarkdown} data-testid={`journal-entry-${entry.id}-content`} />
      </div>
      {(onEdit || canAsk) && (
        <div className="flex shrink-0 items-center gap-1">
          {canAsk && (
            <AskAdvocateButton
              childId={entry.childProfileId}
              about={{ kind: 'journal', id: entry.id }}
              label={`journal entry from ${day}`}
              appearance="icon"
              ariaLabel={`Ask the advocate about the update from ${day}`}
              className="border-transparent"
              data-testid={`journal-entry-${entry.id}-ask`}
            />
          )}
          {onEdit && (
            <Button
              variant="ghost"
              size="sm"
              className="shrink-0"
              aria-label={`Edit update from ${day}`}
              onClick={() => onEdit(entry)}
              data-testid={`journal-entry-${entry.id}-edit`}
            >
              <Pencil className="h-4 w-4" aria-hidden="true" />
            </Button>
          )}
        </div>
      )}
    </li>
  );
}
