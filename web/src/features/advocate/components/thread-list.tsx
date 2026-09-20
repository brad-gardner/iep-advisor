import { useState, type FormEvent } from 'react';
import { MessageSquare, Pencil, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Input } from '@/components/ui/input';
import { Menu } from '@/components/ui/menu';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { cn } from '@/lib/cn';
import { relativeTime } from '@/lib/relative-time';
import { ADVOCATE_TITLE_MAX_LENGTH, type AdvocateThreadDto } from '../types/advocate';

interface ThreadListProps {
  threads: AdvocateThreadDto[] | null;
  error: string | null;
  selectedId: number | null;
  onSelect: (id: number) => void;
  onNew: () => void;
  /** Resolve on success; reject with a user-facing `Error` to keep the dialog open. */
  onRename: (id: number, title: string) => Promise<void>;
  onDelete: (id: number) => Promise<void>;
  /** Viewers cannot start or change conversations. */
  canAsk: boolean;
  /** Sends are in flight — starting a new conversation would abandon the answer. */
  busy: boolean;
  /**
   * `panel` (default) draws its own card border/padding — used for the
   * desktop `<aside>` rail, which sits next to the conversation panel and
   * needs a matching frame. `plain` drops that chrome for the phone
   * `<Drawer>`, which already supplies its own border and padding, so the
   * two would otherwise double up.
   */
  variant?: 'panel' | 'plain';
}

/**
 * The conversation rail: newest activity first, the open one marked with
 * `aria-current`. Rename and delete live in a per-row menu and confirm in
 * the shared Modal / ConfirmDialog. The same component renders inside a
 * Drawer on phones and as a side rail from `md` up.
 */
export function ThreadList({
  threads,
  error,
  selectedId,
  onSelect,
  onNew,
  onRename,
  onDelete,
  canAsk,
  busy,
  variant = 'panel',
}: ThreadListProps) {
  const [renaming, setRenaming] = useState<AdvocateThreadDto | null>(null);
  const [deleting, setDeleting] = useState<AdvocateThreadDto | null>(null);

  return (
    <div
      className={cn('space-y-3', variant === 'panel' && 'rounded-card border border-brand-slate-200 bg-white p-3')}
      data-testid="advocate-thread-list"
    >
      <div className="flex items-center justify-between gap-2 px-1">
        <p className="text-[11px] font-medium uppercase tracking-wide text-brand-slate-500">Conversations</p>
        {canAsk && (
          <button
            type="button"
            onClick={onNew}
            disabled={busy}
            className="flex items-center gap-1 rounded-button px-2 py-1 text-xs font-medium text-brand-teal-600 transition-colors hover:bg-brand-teal-50 focus:outline-none focus:ring-1 focus:ring-brand-teal-400 focus:ring-offset-1 disabled:cursor-not-allowed disabled:opacity-50"
            data-testid="advocate-new-thread"
          >
            <Plus className="h-3.5 w-3.5" aria-hidden="true" />
            New
          </button>
        )}
      </div>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      {threads === null && !error && (
        <div className="flex justify-center py-6">
          <Spinner size="sm" label="Loading conversations…" />
        </div>
      )}

      {threads && threads.length === 0 && (
        <p className="px-1 text-xs text-brand-slate-400" data-testid="advocate-thread-list-empty">
          No conversations yet.
        </p>
      )}

      {threads && threads.length > 0 && (
        <ul className="space-y-1" aria-label="Conversations">
          {threads.map((t) => {
            const selected = t.id === selectedId;
            return (
              <li key={t.id} className="flex items-center gap-1" data-testid={`advocate-thread-${t.id}`}>
                <button
                  type="button"
                  onClick={() => onSelect(t.id)}
                  aria-current={selected ? 'true' : undefined}
                  className={cn(
                    'flex min-w-0 flex-1 items-start gap-2 rounded-button px-2 py-1.5 text-left transition-colors focus:outline-none focus:ring-1 focus:ring-brand-teal-400 focus:ring-offset-1',
                    selected ? 'bg-brand-teal-50 text-brand-slate-800' : 'text-brand-slate-600 hover:bg-brand-slate-100',
                  )}
                  data-testid={`advocate-thread-${t.id}-open`}
                >
                  <MessageSquare className="mt-0.5 h-4 w-4 shrink-0 text-brand-slate-400" aria-hidden="true" />
                  <span className="min-w-0 flex-1">
                    <span className="line-clamp-2 text-sm font-medium">{t.title}</span>
                    <span className="block text-[11px] text-brand-slate-400">{relativeTime(t.lastMessageAt)}</span>
                  </span>
                </button>
                {canAsk && (
                  <Menu
                    label={`Actions for ${t.title}`}
                    align="right"
                    data-testid={`advocate-thread-${t.id}-menu`}
                    items={[
                      {
                        label: 'Rename',
                        icon: <Pencil className="h-4 w-4" aria-hidden="true" />,
                        onSelect: () => setRenaming(t),
                        'data-testid': `advocate-thread-${t.id}-rename`,
                      },
                      {
                        label: 'Delete',
                        variant: 'danger',
                        icon: <Trash2 className="h-4 w-4" aria-hidden="true" />,
                        onSelect: () => setDeleting(t),
                        'data-testid': `advocate-thread-${t.id}-delete`,
                      },
                    ]}
                  />
                )}
              </li>
            );
          })}
        </ul>
      )}

      <RenameThreadModal thread={renaming} onClose={() => setRenaming(null)} onRename={onRename} />
      <DeleteThreadDialog thread={deleting} onClose={() => setDeleting(null)} onDelete={onDelete} />
    </div>
  );
}

interface RenameThreadModalProps {
  thread: AdvocateThreadDto | null;
  onClose: () => void;
  onRename: (id: number, title: string) => Promise<void>;
}

function RenameThreadModal({ thread, onClose, onRename }: RenameThreadModalProps) {
  return (
    <Modal open={thread !== null} onClose={onClose} title="Rename conversation" size="sm" data-testid="advocate-rename-dialog">
      {thread && <RenameThreadForm thread={thread} onClose={onClose} onRename={onRename} />}
    </Modal>
  );
}

function RenameThreadForm({ thread, onClose, onRename }: RenameThreadModalProps & { thread: AdvocateThreadDto }) {
  const [title, setTitle] = useState(thread.title);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const clean = title.trim();
  const canSave = clean.length > 0 && clean.length <= ADVOCATE_TITLE_MAX_LENGTH && !saving;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!canSave) return;
    setSaving(true);
    setError(null);
    try {
      await onRename(thread.id, clean);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not rename this conversation.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={(e) => void submit(e)} className="space-y-4" data-testid="advocate-rename-form">
      <Input
        id="advocate-thread-title"
        label="Title"
        value={title}
        maxLength={ADVOCATE_TITLE_MAX_LENGTH}
        onChange={(e) => setTitle(e.target.value)}
        autoFocus
        data-testid="advocate-rename-input"
      />
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" size="sm" onClick={onClose} disabled={saving}>
          Cancel
        </Button>
        <Button type="submit" size="sm" loading={saving} disabled={!canSave} data-testid="advocate-rename-save">
          Save
        </Button>
      </div>
    </form>
  );
}

interface DeleteThreadDialogProps {
  thread: AdvocateThreadDto | null;
  onClose: () => void;
  onDelete: (id: number) => Promise<void>;
}

function DeleteThreadDialog({ thread, onClose, onDelete }: DeleteThreadDialogProps) {
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const runDelete = async () => {
    if (!thread || deleting) return;
    setDeleting(true);
    setError(null);
    try {
      await onDelete(thread.id);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete this conversation.');
    } finally {
      setDeleting(false);
    }
  };

  return (
    <ConfirmDialog
      open={thread !== null}
      title="Delete conversation"
      message={`Delete "${thread?.title ?? ''}"? The advocate's answers in it will be gone. This cannot be undone.`}
      confirmLabel="Delete conversation"
      loading={deleting}
      error={error}
      onConfirm={() => void runDelete()}
      onCancel={() => {
        if (deleting) return;
        setError(null);
        onClose();
      }}
      data-testid="advocate-delete-dialog"
    />
  );
}
