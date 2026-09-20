import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';

const MIN_REASON_LENGTH = 10;
const REASON_MAX_LENGTH = 1000;

interface RemoveGoalDialogProps {
  open: boolean;
  /** The goal row's text, shown so the reviewer knows which goal they're retiring. */
  goalLabel: string;
  loading?: boolean;
  error?: string | null;
  onConfirm: (reason: string) => void;
  onCancel: () => void;
}

/**
 * Removing a row from a `FieldSemantics.Goals` table on a Draft/Finalizing
 * document requires a reason (plan 7, decision 6) — persisted via
 * `POST /api/documents/{instanceId}/goal-retirements` BEFORE the row is
 * actually removed from the field's value array. Cancel leaves the row in
 * place. The parent (`TableField`) owns the actual removal and the API call;
 * this dialog only validates and hands back the trimmed reason.
 */
export function RemoveGoalDialog({ open, goalLabel, loading = false, error, onConfirm, onCancel }: RemoveGoalDialogProps) {
  const [reason, setReason] = useState('');
  const trimmed = reason.trim();
  const canSubmit = trimmed.length >= MIN_REASON_LENGTH && !isMarkdownOverLimit(reason, REASON_MAX_LENGTH);

  const handleCancel = () => {
    setReason('');
    onCancel();
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    onConfirm(trimmed);
  };

  return (
    <Modal
      open={open}
      onClose={handleCancel}
      preventClose={loading}
      title="Remove goal"
      size="sm"
      data-testid="remove-goal-dialog"
      footer={
        <>
          <Button variant="ghost" onClick={handleCancel} disabled={loading} data-testid="remove-goal-dialog-cancel">
            Cancel
          </Button>
          <Button
            variant="danger"
            onClick={handleSubmit}
            loading={loading}
            disabled={!canSubmit}
            data-testid="remove-goal-dialog-confirm"
          >
            Remove goal
          </Button>
        </>
      }
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <p className="text-sm text-brand-slate-600">
          Removing "{goalLabel || 'this goal'}" retires it from the student's record. This is kept as
          part of the goal's history and cannot be undone from here.
        </p>

        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}

        <RichTextEditor
          label="Reason for removing this goal *"
          value={reason}
          onChange={setReason}
          minRows={3}
          maxLength={REASON_MAX_LENGTH}
          required
          data-testid="remove-goal-dialog-reason"
        />
        <p className="text-xs text-brand-slate-500">At least {MIN_REASON_LENGTH} characters.</p>
      </form>
    </Modal>
  );
}
