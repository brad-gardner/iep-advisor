import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor, isMarkdownOverLimit } from '@/components/ui/rich-text-editor';
import { apiErrorMessage } from '@/lib/api-error';
import { useToast } from '@/components/ui/toast';
import { addGoalObservation } from '../api/goals-api';
import type { GoalObservationDto } from '../types';

interface LogProgressFormProps {
  goalRecordId: number;
  onLogged: (observation: GoalObservationDto) => void;
  onCancel: () => void;
  'data-testid'?: string;
}

const NOTE_MAX_LENGTH = 2000;

/**
 * The C11 provider slice: a sub-60-second progress log — value, unit, note
 * (at most three fields) — rather than a full observation editor. The server
 * requires `value` or `note`; the client mirrors that so a truly empty
 * submission never round-trips.
 */
export function LogProgressForm({ goalRecordId, onLogged, onCancel, 'data-testid': testId }: LogProgressFormProps) {
  const { t } = useTranslation(['goals', 'common']);
  const { show: showToast } = useToast();
  const [value, setValue] = useState('');
  const [unit, setUnit] = useState('');
  const [note, setNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const trimmedValue = value.trim();
    const trimmedNote = note.trim();
    if (!trimmedValue && !trimmedNote) {
      setError(t('goals:logProgressForm.valueOrNoteRequired'));
      return;
    }
    if (isMarkdownOverLimit(note, NOTE_MAX_LENGTH)) {
      setError(t('goals:logProgressForm.noteMaxLength', { max: NOTE_MAX_LENGTH }));
      return;
    }
    const parsedValue = trimmedValue ? Number(trimmedValue) : undefined;
    if (trimmedValue && Number.isNaN(parsedValue)) {
      setError(t('goals:logProgressForm.valueMustBeNumber'));
      return;
    }
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await addGoalObservation(goalRecordId, {
        value: parsedValue,
        unit: unit.trim() || undefined,
        note: trimmedNote || undefined,
      });
      if (res.success && res.data) {
        onLogged(res.data);
        showToast({ message: t('goals:logProgressForm.toastLogged'), variant: 'success' });
      } else {
        setError(res.message ?? t('goals:logProgressForm.logFailed'));
      }
    } catch (err) {
      setError(apiErrorMessage(err, t('goals:logProgressForm.logFailed')));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3" data-testid={testId}>
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}
      <div className="grid grid-cols-2 gap-3">
        <Input
          label={t('goals:logProgressForm.valueLabel')}
          inputMode="decimal"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          data-testid={testId ? `${testId}-value` : undefined}
        />
        <Input
          label={t('goals:logProgressForm.unitLabel')}
          placeholder={t('goals:logProgressForm.unitPlaceholder')}
          maxLength={32}
          value={unit}
          onChange={(e) => setUnit(e.target.value)}
          data-testid={testId ? `${testId}-unit` : undefined}
        />
      </div>
      <RichTextEditor
        label={t('goals:logProgressForm.noteLabel')}
        minRows={2}
        maxLength={NOTE_MAX_LENGTH}
        value={note}
        onChange={setNote}
        data-testid={testId ? `${testId}-note` : undefined}
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
          data-testid={testId ? `${testId}-submit` : undefined}
        >
          {t('goals:logProgressForm.submit')}
        </Button>
      </div>
    </form>
  );
}
