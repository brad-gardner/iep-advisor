import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
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

/**
 * The C11 provider slice: a sub-60-second progress log — value, unit, note
 * (at most three fields) — rather than a full observation editor. The server
 * requires `value` or `note`; the client mirrors that so a truly empty
 * submission never round-trips.
 */
export function LogProgressForm({ goalRecordId, onLogged, onCancel, 'data-testid': testId }: LogProgressFormProps) {
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
      setError('Enter a value or a note.');
      return;
    }
    const parsedValue = trimmedValue ? Number(trimmedValue) : undefined;
    if (trimmedValue && Number.isNaN(parsedValue)) {
      setError('Value must be a number.');
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
        showToast({ message: 'Progress logged', variant: 'success' });
      } else {
        setError(res.message ?? 'Could not log progress.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not log progress.'));
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
          label="Value"
          inputMode="decimal"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          data-testid={testId ? `${testId}-value` : undefined}
        />
        <Input
          label="Unit"
          placeholder="e.g. % correct"
          maxLength={32}
          value={unit}
          onChange={(e) => setUnit(e.target.value)}
          data-testid={testId ? `${testId}-unit` : undefined}
        />
      </div>
      <RichTextEditor
        label="Note"
        minRows={2}
        maxLength={2000}
        value={note}
        onChange={setNote}
        data-testid={testId ? `${testId}-note` : undefined}
      />
      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" size="sm" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
        <Button type="submit" size="sm" loading={isSubmitting} data-testid={testId ? `${testId}-submit` : undefined}>
          Log progress
        </Button>
      </div>
    </form>
  );
}
