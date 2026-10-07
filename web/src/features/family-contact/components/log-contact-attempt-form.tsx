import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { apiErrorMessage } from '@/lib/api-error';
import { familyContactMethodLabel, familyContactOutcomeLabel } from '@/lib/family-contact-label';
import { toDateInputValue } from '@/lib/format-date';
import { recordContactAttempt } from '../api/family-contact-api';
import { FAMILY_CONTACT_METHODS, FAMILY_CONTACT_OUTCOMES } from '../types';
import type { FamilyContactAttemptDto, FamilyContactMethod, FamilyContactOutcome } from '../types';

interface LogContactAttemptFormProps {
  studentId: number;
  onLogged: (attempt: FamilyContactAttemptDto) => void;
  onCancel: () => void;
}

const NOTE_MAX_LENGTH = 1000;

/** Log an attempt to reach the family: method, outcome, date, and an optional
 *  note (plan 7, decision 7). */
export function LogContactAttemptForm({ studentId, onLogged, onCancel }: LogContactAttemptFormProps) {
  const { t } = useTranslation(['family-contact', 'common']);
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
        setError(res.message ?? t('logForm.saveFailed'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('logForm.saveFailed')));
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
          label={t('logForm.methodLabel')}
          value={method}
          onChange={(e) => setMethod(e.target.value as FamilyContactMethod)}
          data-testid="contact-attempt-method"
        >
          {FAMILY_CONTACT_METHODS.map((m) => (
            <option key={m} value={m}>
              {familyContactMethodLabel(m)}
            </option>
          ))}
        </Select>
        <Select
          label={t('logForm.outcomeLabel')}
          value={outcome}
          onChange={(e) => setOutcome(e.target.value as FamilyContactOutcome)}
          data-testid="contact-attempt-outcome"
        >
          {FAMILY_CONTACT_OUTCOMES.map((o) => (
            <option key={o} value={o}>
              {familyContactOutcomeLabel(o)}
            </option>
          ))}
        </Select>
        <Input
          label={t('logForm.dateLabel')}
          type="date"
          value={attemptedAt}
          onChange={(e) => setAttemptedAt(e.target.value)}
          data-testid="contact-attempt-date"
        />
      </div>
      <RichTextEditor
        label={t('logForm.noteLabel')}
        value={note}
        onChange={setNote}
        minRows={2}
        maxLength={NOTE_MAX_LENGTH}
        data-testid="contact-attempt-note"
      />
      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel} disabled={isSubmitting}>
          {t('common:ui.cancel')}
        </Button>
        <Button
          type="submit"
          size="sm"
          loading={isSubmitting}
          disabled={isMarkdownOverLimit(note, NOTE_MAX_LENGTH)}
          data-testid="contact-attempt-submit"
        >
          {t('logForm.submitButton')}
        </Button>
      </div>
    </form>
  );
}
