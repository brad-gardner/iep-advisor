import { Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Markdown } from '@/components/ui/markdown';
import { formatDate } from '@/lib/format-date';
import type { JournalEntryDto } from '../types/journal';
import { JournalTagBadge } from './journal-tag-badge';

interface JournalEntryItemProps {
  entry: JournalEntryDto;
  /** Present only when the viewer may edit — opens the entry in the drawer. */
  onEdit?: (entry: JournalEntryDto) => void;
}

/** One dated update: day, tag chip, the note as rendered markdown, and an edit affordance. */
export function JournalEntryItem({ entry, onEdit }: JournalEntryItemProps) {
  const day = formatDate(entry.occurredOn);
  return (
    <li className="flex items-start justify-between gap-3 py-3" data-testid={`journal-entry-${entry.id}`}>
      <div className="min-w-0 flex-1">
        <div className="mb-1 flex flex-wrap items-center gap-2">
          <time dateTime={entry.occurredOn} className="text-[11px] font-semibold uppercase tracking-wide text-brand-slate-500">
            {day}
          </time>
          <JournalTagBadge tag={entry.tag} data-testid={`journal-entry-${entry.id}-tag`} />
        </div>
        <Markdown content={entry.contentMarkdown} data-testid={`journal-entry-${entry.id}-content`} />
      </div>
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
    </li>
  );
}
