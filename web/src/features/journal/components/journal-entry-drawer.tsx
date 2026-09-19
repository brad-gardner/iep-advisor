import { useState, type FormEvent } from 'react';
import { Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Drawer } from '@/components/ui/drawer';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { apiErrorMessage } from '@/lib/api-error';
import { createJournalEntry, deleteJournalEntry, updateJournalEntry } from '../api/journal-api';
import { useJournalLinkOptions, type JournalLinkOption } from '../hooks/use-journal-link-options';
import { todayInputValue } from '../lib/today';
import {
  JOURNAL_CONTENT_MAX_LENGTH,
  JOURNAL_TAGS,
  JOURNAL_TAG_LABELS,
  type JournalEntryDto,
  type JournalTag,
  type SaveJournalEntryRequest,
} from '../types/journal';

export type JournalSaveMode = 'created' | 'updated';

/** Starting values for a *new* entry (e.g. a note the advocate suggested); ignored when editing. */
export type JournalEntryDraft = Partial<Pick<SaveJournalEntryRequest, 'occurredOn' | 'tag' | 'contentMarkdown'>>;

interface JournalEntryDrawerProps {
  open: boolean;
  onClose: () => void;
  childId: number;
  /** The entry being edited; omit (or `null`) to add a new one. */
  entry?: JournalEntryDto | null;
  initial?: JournalEntryDraft;
  onSaved: (entry: JournalEntryDto, mode: JournalSaveMode) => void;
  onDeleted?: (id: number) => void;
  'data-testid'?: string;
}

/**
 * Add/edit one dated journal entry. The form itself lives in `JournalEntryForm`,
 * which the Drawer mounts only while open — so every opening starts from the
 * given `entry` (or blank) without any reset-on-open effect. Esc/backdrop/X are
 * inert while a save or delete is in flight.
 */
export function JournalEntryDrawer({
  open,
  onClose,
  childId,
  entry,
  initial,
  onSaved,
  onDeleted,
  'data-testid': testId = 'journal-entry-drawer',
}: JournalEntryDrawerProps) {
  const [busy, setBusy] = useState(false);
  const editing = Boolean(entry);

  return (
    <Drawer
      open={open}
      onClose={onClose}
      preventClose={busy}
      title={editing ? 'Edit update' : 'Add an update'}
      data-testid={testId}
    >
      <JournalEntryForm
        childId={childId}
        entry={entry ?? null}
        initial={initial}
        testId={testId}
        onBusyChange={setBusy}
        onCancel={onClose}
        onSaved={onSaved}
        onDeleted={onDeleted}
      />
    </Drawer>
  );
}

interface JournalEntryFormProps {
  childId: number;
  entry: JournalEntryDto | null;
  initial?: JournalEntryDraft;
  testId: string;
  onBusyChange: (busy: boolean) => void;
  onCancel: () => void;
  onSaved: (entry: JournalEntryDto, mode: JournalSaveMode) => void;
  onDeleted?: (id: number) => void;
}

/** `<select>` value for an optional numeric link — '' means "not linked". */
const toSelectValue = (id: number | null | undefined) => (id == null ? '' : String(id));
const fromSelectValue = (value: string): number | null => (value === '' ? null : Number(value));

function JournalEntryForm({
  childId,
  entry,
  initial,
  testId,
  onBusyChange,
  onCancel,
  onSaved,
  onDeleted,
}: JournalEntryFormProps) {
  const today = todayInputValue();
  const [occurredOn, setOccurredOn] = useState(entry?.occurredOn ?? initial?.occurredOn ?? today);
  const [tag, setTag] = useState<JournalTag>(entry?.tag ?? initial?.tag ?? 'Other');
  const [content, setContent] = useState(entry?.contentMarkdown ?? initial?.contentMarkdown ?? '');
  const [linkedIepDocumentId, setLinkedIepDocumentId] = useState<number | null>(entry?.linkedIepDocumentId ?? null);
  const [linkedEtrDocumentId, setLinkedEtrDocumentId] = useState<number | null>(entry?.linkedEtrDocumentId ?? null);
  const [linkedMeetingId, setLinkedMeetingId] = useState<number | null>(entry?.linkedMeetingId ?? null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const { options, loading: optionsLoading } = useJournalLinkOptions(childId);

  const overLimit = isMarkdownOverLimit(content, JOURNAL_CONTENT_MAX_LENGTH);
  const dateValid = /^\d{4}-\d{2}-\d{2}$/.test(occurredOn) && occurredOn <= today;
  const canSubmit = content.trim().length > 0 && !overLimit && dateValid && !saving && !deleting;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    setSaving(true);
    onBusyChange(true);
    setError(null);
    const body: SaveJournalEntryRequest = {
      occurredOn,
      tag,
      contentMarkdown: content.trim(),
      linkedIepDocumentId,
      linkedEtrDocumentId,
      linkedMeetingId,
    };
    try {
      const res = entry ? await updateJournalEntry(entry.id, body) : await createJournalEntry(childId, body);
      if (res.success && res.data) {
        onSaved(res.data, entry ? 'updated' : 'created');
      } else {
        setError(res.message ?? 'Could not save this update.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not save this update.'));
    } finally {
      setSaving(false);
      onBusyChange(false);
    }
  };

  // A failed delete stays inside the open dialog so the person can retry or
  // cancel; only success closes it (same contract as AboutMyChildCard).
  const remove = async () => {
    if (!entry || deleting) return;
    setDeleting(true);
    onBusyChange(true);
    setDeleteError(null);
    try {
      const res = await deleteJournalEntry(entry.id);
      if (res.success) {
        setConfirmingDelete(false);
        onDeleted?.(entry.id);
      } else {
        setDeleteError(res.message ?? 'Could not delete this update.');
      }
    } catch (err) {
      setDeleteError(apiErrorMessage(err, 'Could not delete this update.'));
    } finally {
      setDeleting(false);
      onBusyChange(false);
    }
  };

  return (
    <>
      <form onSubmit={(e) => void submit(e)} className="space-y-4" data-testid={`${testId}-form`}>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Input
            id="journal-occurred-on"
            label="Date"
            type="date"
            value={occurredOn}
            max={today}
            required
            onChange={(e) => setOccurredOn(e.target.value)}
            data-testid={`${testId}-date`}
          />
          <Select
            id="journal-tag"
            label="Kind of update"
            value={tag}
            onChange={(e) => setTag(e.target.value as JournalTag)}
            data-testid={`${testId}-tag`}
          >
            {JOURNAL_TAGS.map((t) => (
              <option key={t} value={t}>
                {JOURNAL_TAG_LABELS[t]}
              </option>
            ))}
          </Select>
        </div>

        <RichTextEditor
          id="journal-content"
          label="What happened"
          placeholder="Sent home early after a meltdown at recess. Called Ms. Rivera — she said…"
          minRows={6}
          value={content}
          onChange={setContent}
          maxLength={JOURNAL_CONTENT_MAX_LENGTH}
          disabled={saving}
          data-testid={`${testId}-content`}
        />

        {!optionsLoading && (options.ieps.length > 0 || options.etrs.length > 0 || options.meetings.length > 0) && (
          <fieldset className="space-y-3">
            <legend className="text-[13px] font-medium text-brand-slate-600">Link to (optional)</legend>
            {options.ieps.length > 0 && (
              <LinkSelect
                id="journal-link-iep"
                label="IEP"
                value={linkedIepDocumentId}
                options={options.ieps}
                onChange={setLinkedIepDocumentId}
                testId={`${testId}-link-iep`}
              />
            )}
            {options.etrs.length > 0 && (
              <LinkSelect
                id="journal-link-etr"
                label="ETR"
                value={linkedEtrDocumentId}
                options={options.etrs}
                onChange={setLinkedEtrDocumentId}
                testId={`${testId}-link-etr`}
              />
            )}
            {options.meetings.length > 0 && (
              <LinkSelect
                id="journal-link-meeting"
                label="Meeting"
                value={linkedMeetingId}
                options={options.meetings}
                onChange={setLinkedMeetingId}
                testId={`${testId}-link-meeting`}
              />
            )}
          </fieldset>
        )}

        {!dateValid && occurredOn !== '' && (
          <p className="text-xs text-brand-danger-700" role="alert" data-testid={`${testId}-date-error`}>
            The date can't be in the future.
          </p>
        )}

        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}

        <div className="flex flex-wrap items-center justify-end gap-2 pt-2">
          {entry && onDeleted && (
            <Button
              type="button"
              variant="danger"
              size="sm"
              className="mr-auto"
              disabled={saving || deleting}
              onClick={() => {
                setDeleteError(null);
                setConfirmingDelete(true);
              }}
              data-testid={`${testId}-delete`}
            >
              <Trash2 className="mr-1 h-4 w-4" aria-hidden="true" />
              Delete
            </Button>
          )}
          <Button type="button" variant="ghost" size="sm" onClick={onCancel} disabled={saving || deleting}>
            Cancel
          </Button>
          <Button type="submit" size="sm" loading={saving} disabled={!canSubmit} data-testid={`${testId}-save`}>
            {entry ? 'Save changes' : 'Save update'}
          </Button>
        </div>
      </form>

      {/* Outside the <form>: the dialog's own buttons are untyped and would otherwise submit it. */}
      <ConfirmDialog
        open={confirmingDelete}
        title="Delete update"
        message="Delete this journal update? This cannot be undone."
        confirmLabel="Delete"
        loading={deleting}
        error={deleteError}
        onConfirm={() => void remove()}
        onCancel={() => {
          if (deleting) return;
          setConfirmingDelete(false);
          setDeleteError(null);
        }}
        data-testid={`${testId}-confirm-delete`}
      />
    </>
  );
}

interface LinkSelectProps {
  id: string;
  label: string;
  value: number | null;
  options: JournalLinkOption[];
  onChange: (id: number | null) => void;
  testId: string;
}

function LinkSelect({ id, label, value, options, onChange, testId }: LinkSelectProps) {
  // Keep a link the options no longer list (a document since removed) rather
  // than silently dropping it the next time this entry is saved.
  const orphan = value != null && !options.some((o) => o.id === value);
  return (
    <Select
      id={id}
      label={label}
      value={toSelectValue(value)}
      onChange={(e) => onChange(fromSelectValue(e.target.value))}
      data-testid={testId}
    >
      <option value="">Not linked</option>
      {orphan && <option value={String(value)}>Linked item #{value}</option>}
      {options.map((o) => (
        <option key={o.id} value={String(o.id)}>
          {o.label}
        </option>
      ))}
    </Select>
  );
}
