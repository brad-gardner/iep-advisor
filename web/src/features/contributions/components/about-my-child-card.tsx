import { useEffect, useState } from 'react';
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
import {
  CONTRIBUTION_KINDS,
  CONTRIBUTION_KIND_LABELS,
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

/**
 * The family's "about my child at home" notes. Each note is private until the
 * parent flips "Visible to the school team" — and the label sits on the note
 * itself, not in a settings page, so nobody is surprised by what the school can
 * see. Shared notes reach the case manager's evidence and AI context.
 */
export function AboutMyChildCard({ childId, childName, canEdit }: AboutMyChildCardProps) {
  const { show } = useToast();
  const [items, setItems] = useState<ParentContributionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
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
        else setError(res.message ?? 'Could not load notes.');
      })
      .catch(() => {
        if (active) setError('Could not load notes.');
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [childId]);

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
        show({ message: shared ? 'Saved and shared with the school team' : 'Saved (private)', variant: 'success' });
      } else {
        show({ message: res.message ?? 'Could not save', variant: 'error' });
      }
    } catch {
      show({ message: 'Could not save', variant: 'error' });
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
        show({ message: res.data.isShared ? 'Now visible to the school team' : 'Now private', variant: 'success' });
      } else {
        show({ message: res.message ?? 'Could not update', variant: 'error' });
      }
    } catch {
      show({ message: 'Could not update', variant: 'error' });
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
        setDeleteError(res.message ?? 'Could not delete this note.');
      }
    } catch {
      setDeleteError('Could not delete this note.');
    } finally {
      setDeleting(false);
    }
  };

  const excerpt = (t: string) => (t.length > 40 ? `${t.slice(0, 40)}…` : t);

  return (
    <Card data-testid="about-my-child-card">
      <div className="mb-1 flex items-center justify-between gap-3">
        <h2 className="font-serif">About {childName} at home</h2>
        {canEdit && !adding && (
          <Button variant="secondary" size="sm" onClick={() => setAdding(true)} data-testid="contribution-add">
            <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
            Add a note
          </Button>
        )}
      </div>
      <p className="mb-4 text-sm text-brand-slate-400">
        Strengths, concerns, what works. Notes are private unless you choose to share one with the school team —
        shared notes help the team (and its AI suggestions) see your child the way you do.
      </p>

      {loading && (
        <div className="space-y-2" role="status" aria-label="Loading notes">
          <Skeleton className="h-5 w-3/4" />
          <span className="sr-only">Loading…</span>
        </div>
      )}
      {error && (
        <Notice variant="error" title="Could not load notes">
          {error}
        </Notice>
      )}

      {adding && (
        <div className="mb-4 space-y-3 rounded-card border border-brand-slate-200 bg-brand-slate-50 p-4" data-testid="contribution-form">
          <Select label="This is" id="contribution-kind" value={kind} onChange={(e) => setKind(e.target.value as ParentContributionKind)}>
            {CONTRIBUTION_KINDS.map((k) => (
              <option key={k} value={k}>
                {CONTRIBUTION_KIND_LABELS[k]}
              </option>
            ))}
          </Select>
          <RichTextEditor
            id="contribution-text"
            label="Note"
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
              className="h-4 w-4 rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-400"
              data-testid="contribution-share"
            />
            Visible to the school team
          </label>
          <div className="flex justify-end gap-2">
            <Button variant="ghost" size="sm" onClick={() => setAdding(false)}>
              Cancel
            </Button>
            <Button
              size="sm"
              loading={saving}
              disabled={!text.trim() || isMarkdownOverLimit(text, NOTE_TEXT_MAX_LENGTH)}
              onClick={() => void submit()}
              data-testid="contribution-save"
            >
              Save
            </Button>
          </div>
        </div>
      )}

      {!loading && !error && items.length === 0 && !adding && (
        <p className="text-sm text-brand-slate-400" data-testid="contributions-empty">
          No notes yet.
        </p>
      )}

      <ul className="divide-y divide-brand-slate-100">
        {items.map((item) => (
          <li key={item.id} className="flex items-start justify-between gap-3 py-3" data-testid={`contribution-${item.id}`}>
            <div className="min-w-0">
              <div className="mb-0.5 flex flex-wrap items-center gap-2 text-[11px]">
                <span className="font-medium uppercase tracking-wide text-brand-teal-600">{CONTRIBUTION_KIND_LABELS[item.kind]}</span>
                <span
                  className={
                    item.isShared
                      ? 'inline-flex items-center gap-1 rounded-full bg-brand-teal-50 px-1.5 text-brand-teal-700'
                      : 'inline-flex items-center gap-1 rounded-full bg-brand-slate-100 px-1.5 text-brand-slate-500'
                  }
                  data-testid={`contribution-${item.id}-visibility`}
                >
                  {item.isShared ? <Eye className="h-3 w-3" aria-hidden="true" /> : <EyeOff className="h-3 w-3" aria-hidden="true" />}
                  {item.isShared ? 'Visible to the school team' : 'Private to your family'}
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
                  aria-label={`${item.isShared ? 'Make private' : 'Share'}: ${excerpt(item.text)}`}
                  onClick={() => void toggleShared(item)}
                  data-testid={`contribution-${item.id}-toggle`}
                >
                  {item.isShared ? 'Make private' : 'Share'}
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={`Delete note: ${excerpt(item.text)}`}
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
        title="Delete note"
        message="Delete this note? If it was shared, the school team will no longer see it."
        confirmLabel="Delete"
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
