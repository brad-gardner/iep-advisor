import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { listJournalEntries } from '../api/journal-api';
import { JOURNAL_EMPTY_COPY } from '../lib/copy';
import type { JournalEntryDto } from '../types/journal';
import { JournalEntryDrawer, type JournalSaveMode } from './journal-entry-drawer';
import { JournalEntryItem } from './journal-entry-item';

interface JournalCardProps {
  childId: number;
  childName: string;
  /** Owners/collaborators can write; viewers only read. */
  canEdit: boolean;
}

const RECENT_COUNT = 5;

/**
 * Overview-tab window onto the family's private journal: the five most recent
 * updates (newest day first), an "Add update" launcher, and a link to the full
 * page. Saves go through the shared drawer and then re-read the list, so the
 * card always shows the server's own ordering rather than a local guess.
 */
export function JournalCard({ childId, childName, canEdit }: JournalCardProps) {
  const { show } = useToast();
  const [items, setItems] = useState<JournalEntryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  // `undefined` = drawer closed; `null` = adding; an entry = editing it.
  const [editing, setEditing] = useState<JournalEntryDto | null | undefined>(undefined);

  useEffect(() => {
    let active = true;
    listJournalEntries(childId, { take: RECENT_COUNT })
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
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [childId, reloadToken]);

  const refresh = () => setReloadToken((t) => t + 1);

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

  const journalHref = `/children/${childId}/journal`;

  return (
    <Card data-testid="journal-card">
      <div className="mb-1 flex items-center justify-between gap-3">
        <h2 className="font-serif">{childName}'s journal</h2>
        {canEdit && (
          <Button variant="secondary" size="sm" onClick={() => setEditing(null)} data-testid="journal-add">
            <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
            Add update
          </Button>
        )}
      </div>
      <p className="mb-4 text-sm text-brand-slate-400">
        A dated record of what happens — private to your family, never shared with the school team.
      </p>

      {loading && (
        <div className="space-y-2" role="status" aria-label="Loading journal">
          <Skeleton className="h-5 w-1/3" />
          <Skeleton className="h-5 w-3/4" />
          <span className="sr-only">Loading…</span>
        </div>
      )}
      {error && (
        <Notice variant="error" title="Could not load the journal">
          {error}
          <Button variant="secondary" size="sm" className="mt-2" onClick={refresh}>
            Try again
          </Button>
        </Notice>
      )}

      {!loading && !error && items.length === 0 && (
        <p className="text-sm text-brand-slate-500" data-testid="journal-empty">
          {JOURNAL_EMPTY_COPY}
        </p>
      )}

      {items.length > 0 && (
        <ul className="divide-y divide-brand-slate-100" data-testid="journal-recent">
          {items.map((entry) => (
            <JournalEntryItem key={entry.id} entry={entry} onEdit={canEdit ? setEditing : undefined} />
          ))}
        </ul>
      )}

      {!loading && !error && (
        <div className="mt-3 flex justify-end">
          <Link
            to={journalHref}
            className="text-sm font-medium text-brand-teal-600 hover:underline"
            data-testid="journal-see-all"
          >
            See all updates
          </Link>
        </div>
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
    </Card>
  );
}
