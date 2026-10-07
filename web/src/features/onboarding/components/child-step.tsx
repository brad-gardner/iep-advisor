import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { ChildForm } from '@/features/children/components/child-form';
import { createChild } from '@/features/children/api/children-api';
import type { CreateChildProfileRequest } from '@/types/api';

interface ChildStepProps {
  onNext: () => void;
  onSkip: () => void;
}

export function ChildStep({ onNext, onSkip }: ChildStepProps) {
  const { t } = useTranslation('onboarding');
  const handleSubmit = async (
    data: CreateChildProfileRequest
  ): Promise<{ success: boolean; error?: string }> => {
    try {
      const response = await createChild(data);
      if (response.success) {
        onNext();
        return { success: true };
      }
      return { success: false, error: response.message || t('child.createFailed') };
    } catch {
      return { success: false, error: t('child.genericError') };
    }
  };

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="font-serif text-2xl text-brand-slate-800">
          {t('child.heading')}
        </h1>
        <p className="text-sm text-brand-slate-500 leading-relaxed">
          {t('child.body')}
        </p>
      </div>

      <ChildForm onSubmit={handleSubmit} submitLabel={t('child.submitLabel')} />

      <div className="flex justify-start">
        <Button variant="ghost" onClick={onSkip} data-testid="onboarding-skip-child">
          {t('child.skip')}
        </Button>
      </div>
    </div>
  );
}
