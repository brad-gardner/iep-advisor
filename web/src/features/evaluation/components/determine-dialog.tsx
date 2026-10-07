import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { useAuthoredVersions } from '@/features/document-authoring/hooks/use-authored-versions';
import { apiErrorMessage } from '@/lib/api-error';
import { eligibilityOutcomeLabel } from '@/lib/evaluation-case-label';
import { determineEvaluation } from '../api/evaluation-api';
import { ELIGIBILITY_OUTCOMES } from '../types';
import type { EligibilityOutcome, EvaluationCaseDto } from '../types';

interface DetermineDialogProps {
  open: boolean;
  studentId: number;
  onClose: () => void;
  onDetermined: (updated: EvaluationCaseDto) => void;
}

const RATIONALE_MAX_LENGTH = 4000;

/** Record the determination: outcome, date, rationale, and — when eligible —
 *  optionally the finalized ETR version the determination is based on. */
export function DetermineDialog({ open, studentId, onClose, onDetermined }: DetermineDialogProps) {
  const { t } = useTranslation(['evaluation', 'common']);
  const { versions } = useAuthoredVersions(studentId);
  const etrVersions = versions.filter((v) => v.documentTypeKey === 'ETR');

  const [outcome, setOutcome] = useState<EligibilityOutcome>('Eligible');
  const [determinationDate, setDeterminationDate] = useState('');
  const [rationale, setRationale] = useState('');
  const [etrAuthoredVersionId, setEtrAuthoredVersionId] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit =
    determinationDate !== '' &&
    rationale.trim().length > 0 &&
    !isMarkdownOverLimit(rationale, RATIONALE_MAX_LENGTH);

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
        setError(res.message ?? t('determineDialog.saveFailed'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('determineDialog.saveFailed')));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={handleClose}
      preventClose={isSubmitting}
      title={t('determineDialog.title')}
      data-testid="determine-dialog"
      footer={
        <>
          <Button variant="ghost" onClick={handleClose} disabled={isSubmitting}>
            {t('common:ui.cancel')}
          </Button>
          <Button
            onClick={handleSubmit}
            loading={isSubmitting}
            disabled={!canSubmit}
            data-testid="determine-dialog-submit"
          >
            {t('determineDialog.submitButton')}
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
            label={t('determineDialog.outcomeLabel')}
            value={outcome}
            onChange={(e) => setOutcome(e.target.value as EligibilityOutcome)}
            data-testid="determine-dialog-outcome"
          >
            {ELIGIBILITY_OUTCOMES.map((o) => (
              <option key={o} value={o}>
                {eligibilityOutcomeLabel(o)}
              </option>
            ))}
          </Select>
          <Input
            label={t('determineDialog.dateLabel')}
            type="date"
            required
            value={determinationDate}
            onChange={(e) => setDeterminationDate(e.target.value)}
            data-testid="determine-dialog-date"
          />
        </div>

        {outcome === 'Eligible' && etrVersions.length > 0 && (
          <Select
            label={t('determineDialog.etrVersionLabel')}
            value={etrAuthoredVersionId}
            onChange={(e) => setEtrAuthoredVersionId(e.target.value)}
            data-testid="determine-dialog-etr-version"
          >
            <option value="">{t('determineDialog.noneOption')}</option>
            {etrVersions.map((v) => (
              <option key={v.id} value={v.id}>
                {t('determineDialog.etrVersionOption', { version: v.versionNumber })}
              </option>
            ))}
          </Select>
        )}

        <RichTextEditor
          label={t('determineDialog.rationaleLabel')}
          value={rationale}
          onChange={setRationale}
          minRows={4}
          maxLength={RATIONALE_MAX_LENGTH}
          required
          data-testid="determine-dialog-rationale"
        />
      </form>
    </Modal>
  );
}
