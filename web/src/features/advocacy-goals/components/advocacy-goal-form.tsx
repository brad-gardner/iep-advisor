import { useState } from 'react';
import { Textarea, Select } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

const CATEGORIES = [
  { value: '', label: 'No category' },
  { value: 'academic', label: 'Academic' },
  { value: 'behavioral', label: 'Behavioral' },
  { value: 'services', label: 'Services' },
  { value: 'placement', label: 'Placement' },
];

interface AdvocacyGoalFormProps {
  initialValues?: { goalText: string; category: string };
  onSubmit: (data: { goalText: string; category?: string }) => Promise<{ success: boolean; error?: string }>;
  onCancel?: () => void;
  submitLabel?: string;
}

export function AdvocacyGoalForm({
  initialValues,
  onSubmit,
  onCancel,
  submitLabel = 'Add Goal',
}: AdvocacyGoalFormProps) {
  const [goalText, setGoalText] = useState(initialValues?.goalText ?? '');
  const [category, setCategory] = useState(initialValues?.category ?? '');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    const trimmed = goalText.trim();
    if (trimmed.length < 10) {
      setError('Goal must be at least 10 characters.');
      return;
    }

    setIsSubmitting(true);
    try {
      const result = await onSubmit({
        goalText: trimmed,
        category: category || undefined,
      });
      if (!result.success) {
        setError(result.error || 'Failed to save goal.');
      } else {
        setGoalText('');
        setCategory('');
      }
    } catch {
      setError('An error occurred.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      {error && <Notice variant="error" title={error} />}

      <div>
        <Textarea
          label="Advocacy Goal"
          id="goal-text"
          value={goalText}
          onChange={(e) => setGoalText(e.target.value)}
          placeholder="Describe your priority for your child (e.g., 'Improve reading fluency to grade level')"
          rows={3}
          maxLength={500}
          data-testid="goal-text-input"
        />
        <p className="text-[11px] text-brand-slate-300 mt-1">{goalText.length}/500 characters</p>
      </div>

      <div className="flex gap-3 items-end">
        <div className="flex-1">
          <Select
            label="Category (optional)"
            id="goal-category"
            value={category}
            onChange={(e) => setCategory((e.target as HTMLSelectElement).value)}
            data-testid="goal-category-select"
          >
            {CATEGORIES.map((c) => (
              <option key={c.value} value={c.value}>
                {c.label}
              </option>
            ))}
          </Select>
        </div>

        <div className="flex gap-2">
          {onCancel && (
            <Button variant="ghost" type="button" onClick={onCancel} data-testid="goal-form-cancel">
              Cancel
            </Button>
          )}
          <Button type="submit" disabled={isSubmitting || goalText.trim().length < 10} data-testid="goal-form-submit">
            {isSubmitting ? 'Saving...' : submitLabel}
          </Button>
        </div>
      </div>
    </form>
  );
}
