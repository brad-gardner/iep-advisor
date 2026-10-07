import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Eye, EyeOff, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Select } from '@/components/ui/input';
import { Markdown } from '@/components/ui/markdown';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/toast';
import { contributionKindLabel } from '@/lib/contribution-label';
import {
  CONTRIBUTION_KINDS,
  createContribution,
  deleteContribution,
  listContributions,
  updateContribution,
  type ParentContributionDto,
  type ParentContributionKind,
} from '../api/contributions-api';

interface AboutMyChildCardProps {
  childId: number;
  childName: string;
  /** Owners/collaborators can write; viewers only read. */
  canEdit: boolean;
}

const NOTE_TEXT_MAX_LENGTH = 2000;

// A server-provided message is already resolved text and is shown as-is;
// the generic fallback is translated at RENDER time (below), not stored
// pre-translated here, so the mount effect never needs `t` in its
// dependency array (same idiom as `useHome`/`MeetingRsvpPage`'s `LoadError`).
type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

/**
 * The family's "about my child at home" notes. Each note is private until the
 * parent flips "Visible to the school team" — and the label sits on the note
 * itself, not in a settings page, so nobody is surprised by what the school can
 * see. Shared notes reach the case manager's evidence and AI context.
 */
export function AboutMyChildCard({ childId, childName, canEdit }: AboutMyChildCardProps) {
  const { t } = useTranslation(['contributions', 'common']);
  const { show } = useToast();
  const [items, setItems] = useState<ParentContributionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<LoadError | null>(null);
  const [adding, setAdding] = useState(false);
  const [kind, setKind] = useState<ParentContributionKind>('Strength');
  const [text, setText] = useState('');
  const [shared, setShared] = useState(false);
  const [saving, setSaving] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<ParentContributionDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  /** Id of the note whose share toggle is in flight — blocks a second overlapping PUT. */
  const [togglingId, setTogglingId] = useState<number | null>(null);

  useEffect(() => {
    let active = true;
    listContributions(childId)
      .then((res) => {
        if (!active) return;
        if (res.success && res.data) setItems(res.data);
        else setError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
      })
      .catch(() => {
        if (active) setError({ kind: 'generic' });
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
    // `t` deliberately excluded (see `use-home.ts`): re-running this fetch on
    // a plain language switch would be wasteful.
  }, [childId]);

  const displayError = error ? (error.kind === 'server' ? error.message : t('card.loadErrorTitle')) : null;

  const submit = async () => {
    if (!text.trim() || isMarkdownOverLimit(text, NOTE_TEXT_MAX_LENGTH)) return;
    setSaving(true);
    try {
      const res = await createContribution(childId, { kind, text: text.trim(), isShared: shared });
      if (res.success && res.data) {
        setItems((cur) => [...cur, res.data!]);
        setText('');
        setShared(false);
        setAdding(false);
        show({
          message: shared ? t('addForm.savedSharedToast') : t('addForm.savedPrivateToast'),
          variant: 'success',
        });
      } else {
        show({ message: res.message ?? t('addForm.saveFailedToast'), variant: 'error' });
      }
    } catch {
      show({ message: t('addForm.saveFailedToast'), variant: 'error' });
    } finally {
      setSaving(false);
    }
  };

  const toggleShared = async (item: ParentContributionDto) => {
    if (togglingId !== null) return;
    setTogglingId(item.id);
    try {
      const res = await updateContribution(item.id, { kind: item.kind, text: item.text, isShared: !item.isShared });
      if (res.success && res.data) {
        setItems((cur) => cur.map((c) => (c.id === item.id ? res.data! : c)));
        show({
          message: res.data.isShared ? t('item.nowVisibleToast') : t('item.nowPrivateToast'),
          variant: 'success',
        });
      } else {
        show({ message: res.message ?? t('item.updateFailedToast'), variant: 'error' });
      }
    } catch {
      show({ message: t('item.updateFailedToast'), variant: 'error' });
    } finally {
      setTogglingId(null);
    }
  };

  // Failures stay inside the open dialog (ConfirmDialog's `error`) so the user
  // can retry or cancel; only a successful delete closes it.
  const remove = async () => {
    if (!confirmDelete || deleting) return;
    setDeleting(true);
    setDeleteError(null);
    try {
      const res = await deleteContribution(confirmDelete.id);
      if (res.success) {
        setItems((cur) => cur.filter((c) => c.id !== confirmDelete.id));
        setConfirmDelete(null);
      } else {
        setDeleteError(res.message ?? t('deleteDialog.deleteFailed'));
      }
    } catch {
      setDeleteError(t('deleteDialog.deleteFailed'));
    } finally {
      setDeleting(false);
    }
  };

  const excerpt = (t: string) => (t.length > 40 ? `${t.slice(0, 40)}…` : t);

  return (
    <Card data-testid="about-my-child-card">
      <div className="mb-1 flex items-center justify-between gap-3">
        <h2 className="font-serif">{t('card.heading', { name: childName })}</h2>
        {canEdit && !adding && (
          <Button variant="secondary" size="sm" onClick={() => setAdding(true)} data-testid="contribution-add">
            <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
            {t('card.addNoteButton')}
          </Button>
        )}
      </div>
      <p className="mb-4 text-sm text-brand-slate-500">{t('card.description')}</p>

      {loading && (
        <div className="space-y-2" role="status" aria-label={t('card.loadingAriaLabel')}>
          <Skeleton className="h-5 w-3/4" />
          <span className="sr-only">{t('common:ui.loading')}</span>
        </div>
      )}
      {displayError && (
        <Notice variant="error" title={t('card.loadErrorTitle')}>
          {displayError}
        </Notice>
      )}

      {adding && (
        <div className="mb-4 space-y-3 rounded-card border border-brand-slate-200 bg-brand-slate-50 p-4" data-testid="contribution-form">
          <Select label={t('addForm.kindLabel')} id="contribution-kind" value={kind} onChange={(e) => setKind(e.target.value as ParentContributionKind)}>
            {CONTRIBUTION_KINDS.map((k) => (
              <option key={k} value={k}>
                {contributionKindLabel(k)}
              </option>
            ))}
          </Select>
          <RichTextEditor
            id="contribution-text"
            label={t('addForm.noteLabel')}
            minRows={3}
            value={text}
            onChange={setText}
            maxLength={NOTE_TEXT_MAX_LENGTH}
          />
          <label className="flex items-center gap-2 text-[13px] font-medium text-brand-slate-600">
            <input
              type="checkbox"
              checked={shared}
              onChange={(e) => setShared(e.target.checked)}
              className="h-4 w-4 rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-500"
              data-testid="contribution-share"
            />
            {t('addForm.sharedLabel')}
          </label>
          <div className="flex justify-end gap-2">
            <Button variant="ghost" size="sm" onClick={() => setAdding(false)}>
              {t('common:ui.cancel')}
            </Button>
            <Button
              size="sm"
              loading={saving}
              disabled={!text.trim() || isMarkdownOverLimit(text, NOTE_TEXT_MAX_LENGTH)}
              onClick={() => void submit()}
              data-testid="contribution-save"
            >
              {t('addForm.saveButton')}
            </Button>
          </div>
        </div>
      )}

      {!loading && !displayError && items.length === 0 && !adding && (
        <p className="text-sm text-brand-slate-500" data-testid="contributions-empty">
          {t('card.noNotesYet')}
        </p>
      )}

      <ul className="divide-y divide-brand-slate-100">
        {items.map((item) => (
          <li key={item.id} className="flex items-start justify-between gap-3 py-3" data-testid={`contribution-${item.id}`}>
            <div className="min-w-0">
              <div className="mb-0.5 flex flex-wrap items-center gap-2 text-[11px]">
                <span className="font-medium uppercase tracking-wide text-brand-teal-600">{contributionKindLabel(item.kind)}</span>
                <span
                  className={
                    item.isShared
                      ? 'inline-flex items-center gap-1 rounded-full bg-brand-teal-50 px-1.5 text-brand-teal-700'
                      : 'inline-flex items-center gap-1 rounded-full bg-brand-slate-100 px-1.5 text-brand-slate-500'
                  }
                  data-testid={`contribution-${item.id}-visibility`}
                >
                  {item.isShared ? <Eye className="h-3 w-3" aria-hidden="true" /> : <EyeOff className="h-3 w-3" aria-hidden="true" />}
                  {item.isShared ? t('item.visibleBadge') : t('item.privateBadge')}
                </span>
              </div>
              <Markdown content={item.text} data-testid={`contribution-${item.id}-text`} />
            </div>
            {canEdit && (
              <div className="flex shrink-0 items-center gap-1">
                <Button
                  variant="ghost"
                  size="sm"
                  loading={togglingId === item.id}
                  disabled={togglingId !== null && togglingId !== item.id}
                  aria-label={t('item.toggleAriaLabel', {
                    action: item.isShared ? t('item.makePrivateAction') : t('item.shareAction'),
                    excerpt: excerpt(item.text),
                  })}
                  onClick={() => void toggleShared(item)}
                  data-testid={`contribution-${item.id}-toggle`}
                >
                  {item.isShared ? t('item.makePrivateAction') : t('item.shareAction')}
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={t('item.deleteAriaLabel', { excerpt: excerpt(item.text) })}
                  onClick={() => {
                    setDeleteError(null);
                    setConfirmDelete(item);
                  }}
                  data-testid={`contribution-${item.id}-delete`}
                >
                  <Trash2 className="h-4 w-4" aria-hidden="true" />
                </Button>
              </div>
            )}
          </li>
        ))}
      </ul>

      <ConfirmDialog
        open={confirmDelete !== null}
        title={t('deleteDialog.title')}
        message={t('deleteDialog.message')}
        confirmLabel={t('deleteDialog.confirmLabel')}
        loading={deleting}
        error={deleteError}
        onConfirm={() => void remove()}
        onCancel={() => {
          if (deleting) return;
          setConfirmDelete(null);
          setDeleteError(null);
        }}
      />
    </Card>
  );
}
