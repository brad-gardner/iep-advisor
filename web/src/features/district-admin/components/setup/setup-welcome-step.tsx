import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';

interface SetupWelcomeStepProps {
  districtName: string;
  onNext: () => void;
  onSkip: () => void;
}

// Step 1: orients a new DistrictAdmin to what the wizard will do. Skippable —
// nothing is created here.
export function SetupWelcomeStep({ districtName, onNext, onSkip }: SetupWelcomeStepProps) {
  const { t } = useTranslation('district-admin');
  return (
    <div className="space-y-6" data-testid="district-setup-welcome">
      <div className="space-y-2">
        <h2 className="font-serif text-2xl text-brand-slate-800">
          {districtName ? t('setupWizard.welcome.headingWithDistrict', { district: districtName }) : t('setupWizard.welcome.heading')}
        </h2>
        <p className="text-sm text-brand-slate-500 leading-relaxed">
          {t('setupWizard.welcome.body')}
        </p>
      </div>

      <div className="flex gap-2">
        <Button onClick={onNext} data-testid="district-setup-next-0">
          {t('setupWizard.welcome.start')}
        </Button>
        <Button variant="ghost" onClick={onSkip} data-testid="district-setup-skip-0">
          {t('setupWizard.welcome.skip')}
        </Button>
      </div>
    </div>
  );
}
