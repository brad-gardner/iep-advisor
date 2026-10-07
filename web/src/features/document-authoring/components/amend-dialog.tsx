import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { apiErrorMessage } from '@/lib/api-error';
import { amendVersion } from '../api/documents-api';

const MIN_REASON_LENGTH = 10;
const REASON_MAX_LENGTH = 1000;

interface AmendDialogProps {
  open: boolean;
  versionId: number;
  onClose: () => void;
  /** Called with the new draft's instance id after a successful amend. */
  onAmended: (instanceId: number) => void;
}

/**
 * Amend a finalized version (plan 7, decision 5): a reason (>= 10 characters)
 * and an optional effective date create a new Draft instance prefilled from
 * the version verbatim (every `_rowId` preserved) — the caller navigates to
 * the new draft's editor on success.
 */
export function AmendDialog({ open, versionId, onClose, onAmended }: AmendDialogProps) {
  const { t } = useTranslation('document-authoring');
  const [reason, setReason] = useState('');
  const [effectiveDate, setEffectiveDate] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const trimmedReason = reason.trim();
  const canSubmit = trimmedReason.length >= MIN_REASON_LENGTH && !isMarkdownOverLimit(reason, REASON_MAX_LENGTH);

  const reset = () => {
    setReason('');
    setEffectiveDate('');
    setError(null);
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await amendVersion(versionId, {
        reason: trimmedReason,
        effectiveDate: effectiveDate || undefined,
      });
      if (res.success && res.data) {
        onAmended(res.data.instanceId);
        reset();
      } else {
        setError(res.message ?? t('amendDialog.genericError'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('amendDialog.genericError')));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={handleClose}
      preventClose={isSubmitting}
      title={t('amendDialog.title')}
      data-testid="amend-dialog"
      footer={
        <>
          <Button variant="ghost" onClick={handleClose} disabled={isSubmitting}>
            {t('amendDialog.cancel')}
          </Button>
          <Button
            onClick={handleSubmit}
            loading={isSubmitting}
            disabled={!canSubmit}
            data-testid="amend-dialog-submit"
          >
            {t('amendDialog.confirm')}
          </Button>
        </>
      }
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <p className="text-sm text-brand-slate-600">{t('amendDialog.body')}</p>

        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}

        <RichTextEditor
          label={t('amendDialog.reasonLabel')}
          value={reason}
          onChange={setReason}
          minRows={3}
          maxLength={REASON_MAX_LENGTH}
          required
          data-testid="amend-dialog-reason"
        />
        <p className="text-xs text-brand-slate-500">{t('amendDialog.minLengthHint', { count: MIN_REASON_LENGTH })}</p>

        <Input
          label={t('amendDialog.effectiveDateLabel')}
          type="date"
          value={effectiveDate}
          onChange={(e) => setEffectiveDate(e.target.value)}
          data-testid="amend-dialog-effective-date"
        />
      </form>
    </Modal>
  );
}
