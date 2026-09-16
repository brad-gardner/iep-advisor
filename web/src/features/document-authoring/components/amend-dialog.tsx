import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Input, Textarea } from '@/components/ui/input';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { apiErrorMessage } from '@/lib/api-error';
import { amendVersion } from '../api/documents-api';

const MIN_REASON_LENGTH = 10;

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
  const [reason, setReason] = useState('');
  const [effectiveDate, setEffectiveDate] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const trimmedReason = reason.trim();
  const canSubmit = trimmedReason.length >= MIN_REASON_LENGTH;

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
        setError(res.message ?? 'Could not amend this version.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not amend this version.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={handleClose}
      preventClose={isSubmitting}
      title="Amend this version"
      data-testid="amend-dialog"
      footer={
        <>
          <Button variant="ghost" onClick={handleClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            loading={isSubmitting}
            disabled={!canSubmit}
            data-testid="amend-dialog-submit"
          >
            Create amendment
          </Button>
        </>
      }
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <p className="text-sm text-brand-slate-600">
          Creates a new draft prefilled from this version. Finalizing it will produce the next version,
          linked back to this one.
        </p>

        {error && (
          <div role="alert">
            <Notice variant="error" title={error} />
          </div>
        )}

        <Textarea
          label="Reason for the amendment *"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          rows={3}
          maxLength={1000}
          required
          data-testid="amend-dialog-reason"
        />
        <p className="text-xs text-brand-slate-400">At least {MIN_REASON_LENGTH} characters.</p>

        <Input
          label="Effective date (optional)"
          type="date"
          value={effectiveDate}
          onChange={(e) => setEffectiveDate(e.target.value)}
          data-testid="amend-dialog-effective-date"
        />
      </form>
    </Modal>
  );
}
