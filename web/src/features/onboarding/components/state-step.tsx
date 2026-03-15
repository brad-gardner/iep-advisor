import { useState } from 'react';
import { Button } from '@/components/ui/button';
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

  const handleContinue = async () => {
    if (!state) return;
    setIsSaving(true);
    await updateProfile({ state });
    setIsSaving(false);
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
        <Button variant="ghost" onClick={onSkip}>
          Skip for now
        </Button>
        <Button onClick={handleContinue} disabled={!state || isSaving}>
          {isSaving ? 'Saving...' : 'Continue'}
        </Button>
      </div>
    </div>
  );
}
