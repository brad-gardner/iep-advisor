import { useEffect, useState } from 'react';
import { useOutletContext } from 'react-router-dom';
import { NotebookPen, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { useToast } from '@/components/ui/toast';
import { usePageTitle } from '@/hooks/use-page-title';
import { apiErrorMessage } from '@/lib/api-error';
import type { ChildOutletContext } from '@/features/children/components/child-detail-page';
import { listJournalEntries } from '../api/journal-api';
import { JOURNAL_EMPTY_COPY } from '../lib/copy';
import { JOURNAL_TAGS, JOURNAL_TAG_LABELS, type JournalEntryDto, type JournalTag } from '../types/journal';
import { JournalEntryDrawer, type JournalSaveMode } from './journal-entry-drawer';
import { JournalEntryItem } from './journal-entry-item';

/** Server-side maximum for one page of entries. */
const PAGE_SIZE = 200;

const TAG_FILTER_ALL = '';

function isJournalTag(value: string): value is JournalTag {
  return (JOURNAL_TAGS as string[]).includes(value);
}

/**
 * `/children/:childId/journal` — every update for the child, newest day first,
 * filterable by kind. Rendered inside the child layout (tab bar above), so the
 * child and the viewer's role come from the outlet context.
 */
export function JournalPage() {
  const { child, childId } = useOutletContext<ChildOutletContext>();
  const canEdit = child.role === 'owner' || child.role === 'collaborator';
  usePageTitle('Journal');
  const { show } = useToast();

  const [tagFilter, setTagFilter] = useState<JournalTag | typeof TAG_FILTER_ALL>(TAG_FILTER_ALL);
  const [items, setItems] = useState<JournalEntryDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [editing, setEditing] = useState<JournalEntryDto | null | undefined>(undefined);

  useEffect(() => {
    let active = true;
    listJournalEntries(childId, { tag: tagFilter || undefined, take: PAGE_SIZE })
      .then((res) => {
        if (!active) return;
        if (res.success && res.data) {
          setItems(res.data);
          setError(null);
        } else {
          setError(res.message ?? 'Could not load the journal.');
        }
      })
      .catch((err) => {
        if (active) setError(apiErrorMessage(err, 'Could not load the journal.'));
      });
    return () => {
      active = false;
    };
  }, [childId, tagFilter, reloadToken]);

  const refresh = () => setReloadToken((t) => t + 1);

  // A new filter is a new list: drop the old rows so the spinner shows instead
  // of the previous kind's entries lingering under the wrong heading.
  const changeFilter = (value: string) => {
    setItems(null);
    setError(null);
    setTagFilter(isJournalTag(value) ? value : TAG_FILTER_ALL);
  };

  const handleSaved = (_entry: JournalEntryDto, mode: JournalSaveMode) => {
    setEditing(undefined);
    show({ message: mode === 'created' ? 'Update added to the journal' : 'Update saved', variant: 'success' });
    refresh();
  };

  const handleDeleted = () => {
    setEditing(undefined);
    show({ message: 'Update deleted', variant: 'success' });
    refresh();
  };

  const loading = items === null && error === null;

  return (
    <div className="space-y-4" data-testid="journal-page">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <h2 className="font-serif">{child.firstName}'s journal</h2>
          <p className="mt-1 text-sm text-brand-slate-400">Private to your family — never shared with the school team.</p>
        </div>
        <div className="flex w-full flex-wrap items-end gap-2 sm:w-auto">
          <div className="min-w-[10rem] flex-1 sm:flex-none">
            <Select
              id="journal-filter-tag"
              label="Show"
              value={tagFilter}
              onChange={(e) => changeFilter(e.target.value)}
              data-testid="journal-filter-tag"
            >
              <option value={TAG_FILTER_ALL}>All updates</option>
              {JOURNAL_TAGS.map((t) => (
                <option key={t} value={t}>
                  {JOURNAL_TAG_LABELS[t]}
                </option>
              ))}
            </Select>
          </div>
          {canEdit && (
            <Button onClick={() => setEditing(null)} data-testid="journal-add">
              <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
              Add update
            </Button>
          )}
        </div>
      </div>

      {loading && (
        <div className="flex justify-center py-12">
          <Spinner label="Loading journal…" />
        </div>
      )}

      {error && (
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button variant="secondary" size="sm" className="mt-2" onClick={refresh}>
              Try again
            </Button>
          </Notice>
        </div>
      )}

      {items && items.length === 0 && (
        <Card>
          <EmptyState
            icon={NotebookPen}
            title={tagFilter ? `No ${JOURNAL_TAG_LABELS[tagFilter].toLowerCase()} updates yet` : 'Nothing in the journal yet'}
            description={JOURNAL_EMPTY_COPY}
            data-testid="journal-empty"
            action={
              canEdit ? (
                <Button onClick={() => setEditing(null)} data-testid="journal-empty-add">
                  <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
                  Add the first update
                </Button>
              ) : undefined
            }
          />
        </Card>
      )}

      {items && items.length > 0 && (
        <Card>
          <ul className="divide-y divide-brand-slate-100" data-testid="journal-list">
            {items.map((entry) => (
              <JournalEntryItem key={entry.id} entry={entry} onEdit={canEdit ? setEditing : undefined} />
            ))}
          </ul>
          {items.length >= PAGE_SIZE && (
            <p className="mt-3 text-xs text-brand-slate-400">Showing the {PAGE_SIZE} most recent updates.</p>
          )}
        </Card>
      )}

      {canEdit && (
        <JournalEntryDrawer
          open={editing !== undefined}
          onClose={() => setEditing(undefined)}
          childId={childId}
          entry={editing ?? null}
          onSaved={handleSaved}
          onDeleted={handleDeleted}
        />
      )}
    </div>
  );
}
