import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
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

const REASON_MAX_LENGTH = 1000;

/** Override the computed determination due date; a reason is required. */
export function DueDateOverrideDialog({ open, studentId, evaluation, onClose, onChanged }: DueDateOverrideDialogProps) {
  const { t } = useTranslation(['evaluation', 'common']);
  const [dueDate, setDueDate] = useState(() => toDateInputValue(evaluation.determinationDueDate));
  const [reason, setReason] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit = dueDate !== '' && reason.trim().length > 0 && !isMarkdownOverLimit(reason, REASON_MAX_LENGTH);

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
        setError(res.message ?? t('dueDateOverrideDialog.saveFailed'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('dueDateOverrideDialog.saveFailed')));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      preventClose={isSubmitting}
      title={t('dueDateOverrideDialog.title')}
      size="sm"
      data-testid="due-date-override-dialog"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={isSubmitting}>
            {t('common:ui.cancel')}
          </Button>
          <Button
            onClick={handleSubmit}
            loading={isSubmitting}
            disabled={!canSubmit}
            data-testid="due-date-override-submit"
          >
            {t('dueDateOverrideDialog.saveButton')}
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
          label={t('dueDateOverrideDialog.newDueDateLabel')}
          type="date"
          required
          value={dueDate}
          onChange={(e) => setDueDate(e.target.value)}
          data-testid="due-date-override-date"
        />
        <RichTextEditor
          label={t('dueDateOverrideDialog.reasonLabel')}
          value={reason}
          onChange={setReason}
          minRows={3}
          maxLength={REASON_MAX_LENGTH}
          required
          data-testid="due-date-override-reason"
        />
      </form>
    </Modal>
  );
}
