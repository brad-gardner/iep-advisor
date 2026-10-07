import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { listJournalEntries } from '../api/journal-api';
import { journalEmptyCopy } from '../lib/copy';
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
/** A server-provided message is already resolved text; the generic case is translated at render time. */
type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

export function JournalCard({ childId, childName, canEdit }: JournalCardProps) {
  const { t } = useTranslation(['journal', 'common']);
  const { show } = useToast();
  const [items, setItems] = useState<JournalEntryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<LoadError | null>(null);
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
          setError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
        }
      })
      .catch((err) => {
        if (!active) return;
        const message = apiErrorMessage(err, '');
        setError(message ? { kind: 'server', message } : { kind: 'generic' });
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [childId, reloadToken]);

  const refresh = () => setReloadToken((t2) => t2 + 1);

  const handleSaved = (_entry: JournalEntryDto, mode: JournalSaveMode) => {
    setEditing(undefined);
    show({ message: mode === 'created' ? t('journal:card.savedCreated') : t('journal:card.savedUpdated'), variant: 'success' });
    refresh();
  };

  const handleDeleted = () => {
    setEditing(undefined);
    show({ message: t('journal:card.deleted'), variant: 'success' });
    refresh();
  };

  const journalHref = `/children/${childId}/journal`;
  const errorMessage = error ? (error.kind === 'server' ? error.message : t('journal:card.loadFailed')) : null;

  return (
    <Card data-testid="journal-card">
      <div className="mb-1 flex items-center justify-between gap-3">
        <h2 className="font-serif">{t('journal:card.heading', { name: childName })}</h2>
        {canEdit && (
          <Button variant="secondary" size="sm" onClick={() => setEditing(null)} data-testid="journal-add">
            <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
            {t('journal:card.addUpdate')}
          </Button>
        )}
      </div>
      <p className="mb-4 text-sm text-brand-slate-500">
        {t('journal:card.description')}
      </p>

      {loading && (
        <div className="space-y-2" role="status" aria-label={t('journal:card.loadingLabel')}>
          <Skeleton className="h-5 w-1/3" />
          <Skeleton className="h-5 w-3/4" />
          <span className="sr-only">{t('common:ui.loading')}</span>
        </div>
      )}
      {errorMessage && (
        <Notice variant="error" title={t('journal:card.loadError')}>
          {errorMessage}
          <Button variant="secondary" size="sm" className="mt-2" onClick={refresh}>
            {t('common:ui.tryAgain')}
          </Button>
        </Notice>
      )}

      {!loading && !errorMessage && items.length === 0 && (
        <p className="text-sm text-brand-slate-500" data-testid="journal-empty">
          {journalEmptyCopy()}
        </p>
      )}

      {items.length > 0 && (
        <ul className="divide-y divide-brand-slate-100" data-testid="journal-recent">
          {items.map((entry) => (
            <JournalEntryItem key={entry.id} entry={entry} onEdit={canEdit ? setEditing : undefined} canAsk={canEdit} />
          ))}
        </ul>
      )}

      {!loading && !errorMessage && (
        <div className="mt-3 flex justify-end">
          <Link
            to={journalHref}
            className="text-sm font-medium text-brand-teal-600 hover:underline"
            data-testid="journal-see-all"
          >
            {t('journal:card.seeAll')}
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
