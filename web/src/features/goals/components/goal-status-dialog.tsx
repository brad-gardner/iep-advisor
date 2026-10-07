import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { apiErrorMessage } from '@/lib/api-error';
import { goalStatusLabel } from '../lib/status-label';
import { updateGoalStatus } from '../api/goals-api';
import type { GoalRecordDto, SettableGoalRecordStatus } from '../types';

const SETTABLE_STATUSES: SettableGoalRecordStatus[] = ['Met', 'NotMet', 'Active'];

interface GoalStatusDialogProps {
  open: boolean;
  goal: GoalRecordDto;
  onClose: () => void;
  onChanged: (updated: GoalRecordDto) => void;
}

const MIN_REASON_LENGTH = 1;
const REASON_MAX_LENGTH = 1000;

/** Mark a goal Met or Not met (or reopen it to Active). The server requires a
 *  reason for Not met; the dialog mirrors that so a blank reason never
 *  round-trips. */
export function GoalStatusDialog({ open, goal, onClose, onChanged }: GoalStatusDialogProps) {
  const { t } = useTranslation(['goals', 'common']);
  const [status, setStatus] = useState<SettableGoalRecordStatus>('Met');
  const [reason, setReason] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reasonRequired = status === 'NotMet';
  const canSubmit =
    (!reasonRequired || reason.trim().length >= MIN_REASON_LENGTH) &&
    !isMarkdownOverLimit(reason, REASON_MAX_LENGTH);

  const reset = () => {
    setStatus('Met');
    setReason('');
    setError(null);
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await updateGoalStatus(goal.id, { status, reason: reason.trim() || undefined });
      if (res.success && res.data) {
        onChanged(res.data);
        handleClose();
      } else {
        setError(res.message ?? t('goals:statusDialog.updateFailed'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('goals:statusDialog.updateFailed')));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={handleClose}
      preventClose={isSubmitting}
      title={t('goals:statusDialog.title')}
      data-testid="goal-status-dialog"
      footer={
        <>
          <Button variant="ghost" onClick={handleClose} disabled={isSubmitting}>
            {t('common:ui.cancel')}
          </Button>
          <Button
            onClick={handleSubmit}
            loading={isSubmitting}
            disabled={!canSubmit}
            data-testid="goal-status-dialog-submit"
          >
            {t('goals:statusDialog.save')}
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

        <fieldset>
          <legend className="mb-1 block text-[13px] font-medium text-brand-slate-600">{t('goals:statusDialog.statusLegend')}</legend>
          <div className="flex flex-wrap gap-3">
            {SETTABLE_STATUSES.map((s) => (
              <label key={s} className="inline-flex items-center gap-1.5 text-sm text-brand-slate-700">
                <input
                  type="radio"
                  name="goal-status-dialog-status"
                  value={s}
                  checked={status === s}
                  onChange={() => setStatus(s)}
                  data-testid={`goal-status-dialog-status-${s}`}
                />
                {goalStatusLabel(s)}
              </label>
            ))}
          </div>
        </fieldset>

        <RichTextEditor
          label={reasonRequired ? t('goals:statusDialog.reasonLabelRequired') : t('goals:statusDialog.reasonLabel')}
          value={reason}
          onChange={setReason}
          minRows={3}
          maxLength={REASON_MAX_LENGTH}
          required={reasonRequired}
          data-testid="goal-status-dialog-reason"
        />
      </form>
    </Modal>
  );
}
