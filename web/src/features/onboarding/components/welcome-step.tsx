import { useTranslation } from 'react-i18next';
import { CheckCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface WelcomeStepProps {
  onNext: () => void;
}

export function WelcomeStep({ onNext }: WelcomeStepProps) {
  const { t } = useTranslation('onboarding');
  return (
    <div className="text-center space-y-6">
      <div className="flex justify-center">
        <div className="bg-brand-teal-50 rounded-full p-4">
          <CheckCircle
            className="text-brand-teal-500"
            size={48}
            strokeWidth={1.8}
            aria-hidden="true"
          />
        </div>
      </div>

      <div className="space-y-3">
        <h1 className="font-serif text-2xl text-brand-slate-800">
          {t('welcome.heading')}
        </h1>
        <p className="text-sm text-brand-slate-500 max-w-md mx-auto leading-relaxed">
          {t('welcome.body')}
        </p>
      </div>

      <Button onClick={onNext} className="mt-4" data-testid="onboarding-start">
        {t('welcome.cta')}
      </Button>
    </div>
  );
}
