import { useState } from 'react';

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
      {error && (
        <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded px-3 py-2">
          {error}
        </div>
      )}

      <div>
        <label htmlFor="goal-text" className="block text-xs text-gray-500 mb-1">
          Advocacy Goal
        </label>
        <textarea
          id="goal-text"
          value={goalText}
          onChange={(e) => setGoalText(e.target.value)}
          placeholder="Describe your priority for your child (e.g., 'Improve reading fluency to grade level')"
          rows={3}
          maxLength={500}
          className="w-full px-3 py-2 bg-white rounded text-gray-900 border border-gray-300 focus:outline-none focus:ring-2 focus:ring-blue-500 text-sm resize-none"
        />
        <p className="text-xs text-gray-400 mt-1">{goalText.length}/500 characters</p>
      </div>

      <div className="flex gap-3 items-end">
        <div className="flex-1">
          <label htmlFor="goal-category" className="block text-xs text-gray-500 mb-1">Category (optional)</label>
          <select
            id="goal-category"
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            className="w-full px-3 py-2 bg-white rounded text-gray-900 border border-gray-300 focus:outline-none focus:ring-2 focus:ring-blue-500 text-sm"
          >
            {CATEGORIES.map((c) => (
              <option key={c.value} value={c.value}>
                {c.label}
              </option>
            ))}
          </select>
        </div>

        <div className="flex gap-2">
          {onCancel && (
            <button
              type="button"
              onClick={onCancel}
              className="px-4 py-2 text-sm text-gray-600 hover:text-gray-900 transition-colors"
            >
              Cancel
            </button>
          )}
          <button
            type="submit"
            disabled={isSubmitting || goalText.trim().length < 10}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 rounded text-sm text-white transition-colors"
          >
            {isSubmitting ? 'Saving...' : submitLabel}
          </button>
        </div>
      </div>
    </form>
  );
}
