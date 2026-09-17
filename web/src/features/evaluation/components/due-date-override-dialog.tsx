import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { apiErrorMessage } from '@/lib/api-error';
import { toDateInputValue } from '@/lib/format-date';
import { overrideDueDate } from '../api/evaluation-api';
import type { EvaluationCaseDto } from '../types';

interface DueDateOverrideDialogProps {
  open: boolean;
  studentId: number;
  evaluation: EvaluationCaseDto;
  onClose: () => void;
  onChanged: (updated: EvaluationCaseDto) => void;
}

/** Override the computed determination due date; a reason is required. */
export function DueDateOverrideDialog({ open, studentId, evaluation, onClose, onChanged }: DueDateOverrideDialogProps) {
  const [dueDate, setDueDate] = useState(() => toDateInputValue(evaluation.determinationDueDate));
  const [reason, setReason] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit = dueDate !== '' && reason.trim().length > 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await overrideDueDate(studentId, { determinationDueDate: dueDate, reason: reason.trim() });
      if (res.success && res.data) {
        onChanged(res.data);
        setReason('');
        onClose();
      } else {
        setError(res.message ?? 'Could not update the due date.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not update the due date.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      preventClose={isSubmitting}
      title="Override determination due date"
      size="sm"
      data-testid="due-date-override-dialog"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            loading={isSubmitting}
            disabled={!canSubmit}
            data-testid="due-date-override-submit"
          >
            Save
          </Button>
        </>
      }
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}
        <Input
          label="New due date *"
          type="date"
          required
          value={dueDate}
          onChange={(e) => setDueDate(e.target.value)}
          data-testid="due-date-override-date"
        />
        <RichTextEditor
          label="Reason *"
          value={reason}
          onChange={setReason}
          minRows={3}
          maxLength={1000}
          required
          data-testid="due-date-override-reason"
        />
      </form>
    </Modal>
  );
}
