import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input, Select, Textarea } from '@/components/ui/input';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { useAuthoredVersions } from '@/features/document-authoring/hooks/use-authored-versions';
import { apiErrorMessage } from '@/lib/api-error';
import { determineEvaluation } from '../api/evaluation-api';
import { ELIGIBILITY_OUTCOME_LABELS, ELIGIBILITY_OUTCOMES } from '../types';
import type { EligibilityOutcome, EvaluationCaseDto } from '../types';

interface DetermineDialogProps {
  open: boolean;
  studentId: number;
  onClose: () => void;
  onDetermined: (updated: EvaluationCaseDto) => void;
}

/** Record the determination: outcome, date, rationale, and — when eligible —
 *  optionally the finalized ETR version the determination is based on. */
export function DetermineDialog({ open, studentId, onClose, onDetermined }: DetermineDialogProps) {
  const { versions } = useAuthoredVersions(studentId);
  const etrVersions = versions.filter((v) => v.documentTypeKey === 'ETR');

  const [outcome, setOutcome] = useState<EligibilityOutcome>('Eligible');
  const [determinationDate, setDeterminationDate] = useState('');
  const [rationale, setRationale] = useState('');
  const [etrAuthoredVersionId, setEtrAuthoredVersionId] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit = determinationDate !== '' && rationale.trim().length > 0;

  const reset = () => {
    setOutcome('Eligible');
    setDeterminationDate('');
    setRationale('');
    setEtrAuthoredVersionId('');
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
      const res = await determineEvaluation(studentId, {
        outcome,
        determinationDate,
        rationale: rationale.trim(),
        etrAuthoredVersionId: etrAuthoredVersionId ? Number(etrAuthoredVersionId) : undefined,
      });
      if (res.success && res.data) {
        onDetermined(res.data);
        handleClose();
      } else {
        setError(res.message ?? 'Could not record the determination.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not record the determination.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={handleClose}
      preventClose={isSubmitting}
      title="Determine eligibility"
      data-testid="determine-dialog"
      footer={
        <>
          <Button variant="ghost" onClick={handleClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            loading={isSubmitting}
            disabled={!canSubmit}
            data-testid="determine-dialog-submit"
          >
            Record determination
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

        <div className="grid gap-4 sm:grid-cols-2">
          <Select
            label="Outcome *"
            value={outcome}
            onChange={(e) => setOutcome(e.target.value as EligibilityOutcome)}
            data-testid="determine-dialog-outcome"
          >
            {ELIGIBILITY_OUTCOMES.map((o) => (
              <option key={o} value={o}>
                {ELIGIBILITY_OUTCOME_LABELS[o]}
              </option>
            ))}
          </Select>
          <Input
            label="Determination date *"
            type="date"
            required
            value={determinationDate}
            onChange={(e) => setDeterminationDate(e.target.value)}
            data-testid="determine-dialog-date"
          />
        </div>

        {outcome === 'Eligible' && etrVersions.length > 0 && (
          <Select
            label="ETR version (optional)"
            value={etrAuthoredVersionId}
            onChange={(e) => setEtrAuthoredVersionId(e.target.value)}
            data-testid="determine-dialog-etr-version"
          >
            <option value="">None</option>
            {etrVersions.map((v) => (
              <option key={v.id} value={v.id}>
                ETR v{v.versionNumber}
              </option>
            ))}
          </Select>
        )}

        <Textarea
          label="Rationale *"
          value={rationale}
          onChange={(e) => setRationale(e.target.value)}
          rows={4}
          maxLength={4000}
          required
          data-testid="determine-dialog-rationale"
        />
      </form>
    </Modal>
  );
}
