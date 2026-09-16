import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { apiErrorMessage } from '@/lib/api-error';
import { createEvaluationCase } from '../api/evaluation-api';
import { EVALUATION_CASE_KIND_LABELS, EVALUATION_CASE_KINDS } from '../types';
import type { EvaluationCaseDto, EvaluationCaseKind } from '../types';

interface StartEvaluationFormProps {
  studentId: number;
  onStarted: (evaluation: EvaluationCaseDto) => void;
}

/** No open (or ever-existing) case yet: start one with a kind, referral date,
 *  and optional source. */
export function StartEvaluationForm({ studentId, onStarted }: StartEvaluationFormProps) {
  const [kind, setKind] = useState<EvaluationCaseKind>('Initial');
  const [referralDate, setReferralDate] = useState('');
  const [referralSource, setReferralSource] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!referralDate) {
      setError('Referral date is required.');
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
        setError(res.message ?? 'Could not start the evaluation.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not start the evaluation.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4" data-testid="start-evaluation-form">
      <p className="text-sm text-brand-slate-600">
        No evaluation case is open for this student yet.
      </p>
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Select
          label="Kind *"
          value={kind}
          onChange={(e) => setKind(e.target.value as EvaluationCaseKind)}
          data-testid="start-evaluation-kind"
        >
          {EVALUATION_CASE_KINDS.map((k) => (
            <option key={k} value={k}>
              {EVALUATION_CASE_KIND_LABELS[k]}
            </option>
          ))}
        </Select>
        <Input
          label="Referral date *"
          type="date"
          required
          value={referralDate}
          onChange={(e) => setReferralDate(e.target.value)}
          data-testid="start-evaluation-referral-date"
        />
      </div>
      <Input
        label="Referral source"
        maxLength={200}
        value={referralSource}
        onChange={(e) => setReferralSource(e.target.value)}
        data-testid="start-evaluation-referral-source"
      />
      <Button type="submit" loading={isSubmitting} data-testid="start-evaluation-submit">
        Start evaluation
      </Button>
    </form>
  );
}
