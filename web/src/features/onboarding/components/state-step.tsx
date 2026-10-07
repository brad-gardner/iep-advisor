import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { StateSelector } from '@/features/auth/components/state-selector';
import { useAuth } from '@/features/auth/hooks/use-auth';

interface StateStepProps {
  onNext: () => void;
  onSkip: () => void;
}

export function StateStep({ onNext, onSkip }: StateStepProps) {
  const { t } = useTranslation('onboarding');
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
      setError(result.error || t('state.saveFailed'));
      return;
    }
    onNext();
  };

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="font-serif text-2xl text-brand-slate-800">
          {t('state.heading')}
        </h1>
        <p className="text-sm text-brand-slate-500 leading-relaxed">
          {t('state.body')}
        </p>
      </div>

      {error && <Notice variant="error" title={error} />}

      <div className="max-w-sm">
        <label
          htmlFor="state"
          className="block text-xs font-medium text-brand-slate-600 mb-1.5"
        >
          {t('state.label')}
        </label>
        <StateSelector value={state} onChange={setState} />
      </div>

      <div className="flex items-center justify-between pt-2">
        <Button variant="ghost" onClick={onSkip} data-testid="onboarding-skip-state">
          {t('state.skip')}
        </Button>
        <Button onClick={handleContinue} loading={isSaving} disabled={!state} data-testid="onboarding-continue-state">
          {t('state.continue')}
        </Button>
      </div>
    </div>
  );
}
