import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import type { SaveSchoolRequest } from '../types';

interface SchoolFormProps {
  // 'create' renders the standalone add card; 'edit' renders a compact inline
  // form for an existing row.
  mode: 'create' | 'edit';
  initialName?: string;
  initialStateCode?: string;
  submitLabel: string;
  onSubmit: (data: SaveSchoolRequest) => Promise<{ success: boolean; error?: string }>;
  onCancel?: () => void;
  testIdPrefix: string;
}

// Two-letter US state code, uppercased as the user types.
function normalizeState(value: string): string {
  return value.replace(/[^a-zA-Z]/g, '').slice(0, 2).toUpperCase();
}

export function SchoolForm({
  mode,
  initialName = '',
  initialStateCode = '',
  submitLabel,
  onSubmit,
  onCancel,
  testIdPrefix,
}: SchoolFormProps) {
  const [name, setName] = useState(initialName);
  const [stateCode, setStateCode] = useState(initialStateCode);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('School name is required');
      return;
    }
    // Server requires a 2-letter code when present; treat a partial entry as
    // "no state" rather than sending an invalid 1-char value.
    const trimmedState = stateCode.trim();
    if (trimmedState.length === 1) {
      setError('Enter a 2-letter state code, or leave it blank');
      return;
    }

    setIsSubmitting(true);
    setError(null);

    const result = await onSubmit({
      name: name.trim(),
      stateCode: trimmedState.length === 2 ? trimmedState : undefined,
    });

    if (result.success) {
      if (mode === 'create') {
        setName('');
        setStateCode('');
      }
    } else {
      setError(result.error ?? 'Something went wrong');
    }
    setIsSubmitting(false);
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="space-y-4"
      data-testid={`${testIdPrefix}-form`}
    >
      {error && <Notice variant="error" title={error} />}

      <Input
        id={`${testIdPrefix}-name`}
        label="School name *"
        required
        value={name}
        onChange={(e) => setName(e.target.value)}
        maxLength={200}
        data-testid={`${testIdPrefix}-name`}
      />

      <Input
        id={`${testIdPrefix}-state`}
        label="State"
        placeholder="e.g. OH"
        value={stateCode}
        onChange={(e) => setStateCode(normalizeState(e.target.value))}
        maxLength={2}
        autoCapitalize="characters"
        className="uppercase"
        data-testid={`${testIdPrefix}-state`}
      />

      <div className="flex gap-2">
        <Button
          type="submit"
          disabled={isSubmitting}
          data-testid={`${testIdPrefix}-submit`}
        >
          {isSubmitting ? 'Saving...' : submitLabel}
        </Button>
        {onCancel && (
          <Button
            type="button"
            variant="ghost"
            onClick={onCancel}
            disabled={isSubmitting}
            data-testid={`${testIdPrefix}-cancel`}
          >
            Cancel
          </Button>
        )}
      </div>
    </form>
  );
}
