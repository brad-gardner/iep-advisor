import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { StateSelector } from '@/features/auth/components/state-selector';
import { useAuth } from '@/features/auth/hooks/use-auth';

interface StateStepProps {
  onNext: () => void;
  onSkip: () => void;
}

export function StateStep({ onNext, onSkip }: StateStepProps) {
  const { user, updateProfile } = useAuth();
  const [state, setState] = useState(user?.state ?? '');
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState('');

  const handleContinue = async () => {
    if (!state) return;
    setError('');
    setIsSaving(true);
    const result = await updateProfile({ state });
    setIsSaving(false);
    if (!result.success) {
      setError(result.error || 'Failed to save state.');
      return;
    }
    onNext();
  };

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="font-serif text-2xl text-brand-slate-800">
          Set Your State
        </h1>
        <p className="text-sm text-brand-slate-500 leading-relaxed">
          Your state determines which IEP laws and regulations apply to your
          child's education. We'll tailor our guidance to your jurisdiction.
        </p>
      </div>

      {error && <Notice variant="error" title={error} />}

      <div className="max-w-sm">
        <label
          htmlFor="state"
          className="block text-xs font-medium text-brand-slate-600 mb-1.5"
        >
          State / Jurisdiction
        </label>
        <StateSelector value={state} onChange={setState} />
      </div>

      <div className="flex items-center justify-between pt-2">
        <Button variant="ghost" onClick={onSkip} data-testid="onboarding-skip-state">
          Skip for now
        </Button>
        <Button onClick={handleContinue} disabled={!state || isSaving} data-testid="onboarding-continue-state">
          {isSaving ? 'Saving...' : 'Continue'}
        </Button>
      </div>
    </div>
  );
}
