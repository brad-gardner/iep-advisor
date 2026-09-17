import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { apiErrorMessage } from '@/lib/api-error';
import { toDateInputValue } from '@/lib/format-date';
import { recordContactAttempt } from '../api/family-contact-api';
import {
  FAMILY_CONTACT_METHODS,
  FAMILY_CONTACT_METHOD_LABELS,
  FAMILY_CONTACT_OUTCOMES,
  FAMILY_CONTACT_OUTCOME_LABELS,
} from '../types';
import type { FamilyContactAttemptDto, FamilyContactMethod, FamilyContactOutcome } from '../types';

interface LogContactAttemptFormProps {
  studentId: number;
  onLogged: (attempt: FamilyContactAttemptDto) => void;
  onCancel: () => void;
}

/** Log an attempt to reach the family: method, outcome, date, and an optional
 *  note (plan 7, decision 7). */
export function LogContactAttemptForm({ studentId, onLogged, onCancel }: LogContactAttemptFormProps) {
  const [method, setMethod] = useState<FamilyContactMethod>('Phone');
  const [outcome, setOutcome] = useState<FamilyContactOutcome>('Reached');
  const [attemptedAt, setAttemptedAt] = useState(toDateInputValue(new Date().toISOString()));
  const [note, setNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await recordContactAttempt(studentId, {
        attemptedAt: attemptedAt || undefined,
        method,
        outcome,
        note: note.trim() || undefined,
      });
      if (res.success && res.data) {
        onLogged(res.data);
      } else {
        setError(res.message ?? 'Could not log this contact attempt.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not log this contact attempt.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3" data-testid="log-contact-attempt-form">
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}
      <div className="grid gap-3 sm:grid-cols-3">
        <Select
          label="Method *"
          value={method}
          onChange={(e) => setMethod(e.target.value as FamilyContactMethod)}
          data-testid="contact-attempt-method"
        >
          {FAMILY_CONTACT_METHODS.map((m) => (
            <option key={m} value={m}>
              {FAMILY_CONTACT_METHOD_LABELS[m]}
            </option>
          ))}
        </Select>
        <Select
          label="Outcome *"
          value={outcome}
          onChange={(e) => setOutcome(e.target.value as FamilyContactOutcome)}
          data-testid="contact-attempt-outcome"
        >
          {FAMILY_CONTACT_OUTCOMES.map((o) => (
            <option key={o} value={o}>
              {FAMILY_CONTACT_OUTCOME_LABELS[o]}
            </option>
          ))}
        </Select>
        <Input
          label="Date"
          type="date"
          value={attemptedAt}
          onChange={(e) => setAttemptedAt(e.target.value)}
          data-testid="contact-attempt-date"
        />
      </div>
      <RichTextEditor
        label="Note (optional)"
        value={note}
        onChange={setNote}
        minRows={2}
        maxLength={1000}
        data-testid="contact-attempt-note"
      />
      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
        <Button type="submit" size="sm" loading={isSubmitting} data-testid="contact-attempt-submit">
          Log attempt
        </Button>
      </div>
    </form>
  );
}
