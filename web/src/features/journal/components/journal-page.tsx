import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useOutletContext, useSearchParams } from 'react-router-dom';
import { NotebookPen, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { useToast } from '@/components/ui/toast';
import { usePageTitle } from '@/hooks/use-page-title';
import { type LoadError, toLoadError, loadErrorText } from '@/lib/api-error';
import type { ChildOutletContext } from '@/features/children/components/child-detail-page';
import { listJournalEntries } from '../api/journal-api';
import { journalEmptyCopy } from '../lib/copy';
import { journalTagLabel } from '../lib/tag-label';
import { JOURNAL_TAGS, type JournalEntryDto, type JournalTag } from '../types/journal';
import { JournalEntryDrawer, type JournalSaveMode } from './journal-entry-drawer';
import { journalEntryElementId } from '../lib/entry-anchor';
import { JournalEntryItem } from './journal-entry-item';

/** Server-side maximum for one page of entries. */
const PAGE_SIZE = 200;

/** `?entry=<id>` — an advocate citation brings one entry into view. */
const ENTRY_PARAM = 'entry';

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
  const { t } = useTranslation(['journal', 'common']);
  const { child, childId } = useOutletContext<ChildOutletContext>();
  const canEdit = child.role === 'owner' || child.role === 'collaborator';
  usePageTitle(t('journal:pageTitle'));
  const { show } = useToast();

  const [searchParams] = useSearchParams();
  const entryParam = searchParams.get(ENTRY_PARAM);
  const targetEntryId = entryParam && /^\d+$/.test(entryParam) ? Number(entryParam) : null;

  const [tagFilter, setTagFilter] = useState<JournalTag | typeof TAG_FILTER_ALL>(TAG_FILTER_ALL);
  const [items, setItems] = useState<JournalEntryDto[] | null>(null);
  const [error, setError] = useState<LoadError | null>(null);
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
          setError(toLoadError(res));
        }
      })
      .catch((err) => {
        if (!active) return;
        setError(toLoadError(err));
      });
    return () => {
      active = false;
    };
  }, [childId, tagFilter, reloadToken]);

  // Once the list holds the deep-linked entry, bring it into view (the row
  // itself is marked `highlighted`). An id not in the list simply shows the list.
  useEffect(() => {
    if (targetEntryId == null || !items) return;
    const el = document.getElementById(journalEntryElementId(targetEntryId));
    if (el && typeof el.scrollIntoView === 'function') el.scrollIntoView({ block: 'center' });
  }, [targetEntryId, items]);

  const refresh = () => setReloadToken((t2) => t2 + 1);

  // A new filter is a new list: drop the old rows so the spinner shows instead
  // of the previous kind's entries lingering under the wrong heading.
  const changeFilter = (value: string) => {
    setItems(null);
    setError(null);
    setTagFilter(isJournalTag(value) ? value : TAG_FILTER_ALL);
  };

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

  const loading = items === null && error === null;
  const errorMessage = loadErrorText(error, t('journal:page.loadFailed'));

  return (
    <div className="space-y-4" data-testid="journal-page">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <h2 className="font-serif">{t('journal:page.heading', { name: child.firstName })}</h2>
          <p className="mt-1 text-sm text-brand-slate-500">{t('journal:page.description')}</p>
        </div>
        <div className="flex w-full flex-wrap items-end gap-2 sm:w-auto">
          <div className="min-w-[10rem] flex-1 sm:flex-none">
            <Select
              id="journal-filter-tag"
              label={t('journal:page.showLabel')}
              value={tagFilter}
              onChange={(e) => changeFilter(e.target.value)}
              data-testid="journal-filter-tag"
            >
              <option value={TAG_FILTER_ALL}>{t('journal:page.allUpdates')}</option>
              {JOURNAL_TAGS.map((tag) => (
                <option key={tag} value={tag}>
                  {journalTagLabel(tag)}
                </option>
              ))}
            </Select>
          </div>
          {canEdit && (
            <Button onClick={() => setEditing(null)} data-testid="journal-add">
              <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
              {t('journal:card.addUpdate')}
            </Button>
          )}
        </div>
      </div>

      {loading && (
        <div className="flex justify-center py-12">
          <Spinner label={t('journal:page.loading')} />
        </div>
      )}

      {errorMessage && (
        <div role="alert">
          <Notice variant="error" title={errorMessage}>
            <Button variant="secondary" size="sm" className="mt-2" onClick={refresh}>
              {t('common:ui.tryAgain')}
            </Button>
          </Notice>
        </div>
      )}

      {items && items.length === 0 && (
        <Card>
          <EmptyState
            icon={NotebookPen}
            title={tagFilter ? t('journal:page.emptyTitleFiltered', { tag: journalTagLabel(tagFilter).toLowerCase() }) : t('journal:page.emptyTitleAll')}
            description={journalEmptyCopy()}
            data-testid="journal-empty"
            action={
              canEdit ? (
                <Button onClick={() => setEditing(null)} data-testid="journal-empty-add">
                  <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
                  {t('journal:page.addFirstUpdate')}
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
              <JournalEntryItem
                key={entry.id}
                entry={entry}
                onEdit={canEdit ? setEditing : undefined}
                canAsk={canEdit}
                highlighted={entry.id === targetEntryId}
              />
            ))}
          </ul>
          {items.length >= PAGE_SIZE && (
            <p className="mt-3 text-xs text-brand-slate-500">{t('journal:page.showingMostRecent', { count: PAGE_SIZE })}</p>
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
