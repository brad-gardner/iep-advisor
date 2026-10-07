import { useTranslation } from 'react-i18next';
import { CheckCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { DistrictSchool } from '../../types';

interface SetupDoneStepProps {
  createdSchool: DistrictSchool | null;
  onFinish: () => void;
}

// Step 4: recap what was set up and hand off to the dashboard.
export function SetupDoneStep({ createdSchool, onFinish }: SetupDoneStepProps) {
  const { t } = useTranslation('district-admin');
  return (
    <div className="space-y-6 text-center" data-testid="district-setup-done">
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

      <div className="space-y-2">
        <h2 className="font-serif text-2xl text-brand-slate-800">{t('setupWizard.done.heading')}</h2>
        <p className="text-sm text-brand-slate-500 max-w-md mx-auto leading-relaxed">
          {createdSchool
            ? t('setupWizard.done.bodyWithSchool', { name: createdSchool.name })
            : t('setupWizard.done.bodyWithoutSchool')}
        </p>
      </div>

      <Button onClick={onFinish} className="mt-2" data-testid="district-setup-finish">
        {t('setupWizard.done.finish')}
      </Button>
    </div>
  );
}
