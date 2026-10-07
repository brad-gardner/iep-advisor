import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { apiErrorMessage } from '@/lib/api-error';
import { evaluationCaseKindLabel } from '@/lib/evaluation-case-label';
import { createEvaluationCase } from '../api/evaluation-api';
import { EVALUATION_CASE_KINDS } from '../types';
import type { EvaluationCaseDto, EvaluationCaseKind } from '../types';

interface StartEvaluationFormProps {
  studentId: number;
  onStarted: (evaluation: EvaluationCaseDto) => void;
}

/** No open (or ever-existing) case yet: start one with a kind, referral date,
 *  and optional source. */
export function StartEvaluationForm({ studentId, onStarted }: StartEvaluationFormProps) {
  const { t } = useTranslation('evaluation');
  const [kind, setKind] = useState<EvaluationCaseKind>('Initial');
  const [referralDate, setReferralDate] = useState('');
  const [referralSource, setReferralSource] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!referralDate) {
      setError(t('startEvaluationForm.referralDateRequired'));
      return;
    }
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await createEvaluationCase(studentId, {
        kind,
        referralDate,
        referralSource: referralSource.trim() || undefined,
      });
      if (res.success && res.data) {
        onStarted(res.data);
      } else {
        setError(res.message ?? t('startEvaluationForm.startFailed'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('startEvaluationForm.startFailed')));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4" data-testid="start-evaluation-form">
      <p className="text-sm text-brand-slate-600">{t('startEvaluationForm.noCaseYet')}</p>
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Select
          label={t('startEvaluationForm.kindLabel')}
          value={kind}
          onChange={(e) => setKind(e.target.value as EvaluationCaseKind)}
          data-testid="start-evaluation-kind"
        >
          {EVALUATION_CASE_KINDS.map((k) => (
            <option key={k} value={k}>
              {evaluationCaseKindLabel(k)}
            </option>
          ))}
        </Select>
        <Input
          label={t('startEvaluationForm.referralDateLabel')}
          type="date"
          required
          value={referralDate}
          onChange={(e) => setReferralDate(e.target.value)}
          data-testid="start-evaluation-referral-date"
        />
      </div>
      <Input
        label={t('startEvaluationForm.referralSourceLabel')}
        maxLength={200}
        value={referralSource}
        onChange={(e) => setReferralSource(e.target.value)}
        data-testid="start-evaluation-referral-source"
      />
      <Button type="submit" loading={isSubmitting} data-testid="start-evaluation-submit">
        {t('startEvaluationForm.submitButton')}
      </Button>
    </form>
  );
}
