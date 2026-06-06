import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';

interface FinalizeDialogProps {
  // The version number this finalize will create, e.g. 1, 2, … for the copy.
  nextVersionNumber?: number;
  isSubmitting: boolean;
  error: string | null;
  onConfirm: (effectiveDate: string | null) => void;
  onCancel: () => void;
}

// Inline confirmation panel for finalizing a draft into an immutable version.
// Captures an optional effective date.
export function FinalizeDialog({
  nextVersionNumber,
  isSubmitting,
  error,
  onConfirm,
  onCancel,
}: FinalizeDialogProps) {
  const [effectiveDate, setEffectiveDate] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onConfirm(effectiveDate.trim() ? effectiveDate : null);
  };

  const versionLabel = nextVersionNumber ? `v${nextVersionNumber}` : 'a new version';

  return (
    <div
      className="bg-brand-slate-50 rounded-card p-4 border border-brand-slate-200"
      data-testid="finalize-dialog"
    >
      <h3 className="font-serif text-brand-slate-800 mb-2">Finalize this IEP</h3>
      <p className="text-sm text-brand-slate-600 mb-3">
        This creates an immutable version ({versionLabel}) of the IEP that parents can view.
        The draft stays editable.
      </p>

      {error && (
        <div className="mb-3">
          <Notice variant="error" title={error} />
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-3">
        <Input
          label="Effective date (optional)"
          type="date"
          value={effectiveDate}
          onChange={(e) => setEffectiveDate(e.target.value)}
          data-testid="finalize-effective-date"
        />

        <div className="flex gap-2 pt-1">
          <Button type="submit" disabled={isSubmitting} data-testid="finalize-confirm">
            {isSubmitting ? 'Finalizing…' : 'Finalize'}
          </Button>
          <Button variant="ghost" type="button" onClick={onCancel} disabled={isSubmitting}>
            Cancel
          </Button>
        </div>
      </form>
    </div>
  );
}
