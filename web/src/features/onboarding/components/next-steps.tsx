import { useTranslation } from 'react-i18next';
import { FileText, Target, ClipboardCheck, GitCompare } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';

interface NextStepsProps {
  onFinish: () => void;
}

const features = [
  { Icon: FileText, titleKey: 'nextSteps.uploadTitle', descriptionKey: 'nextSteps.uploadDescription' },
  { Icon: Target, titleKey: 'nextSteps.goalsTitle', descriptionKey: 'nextSteps.goalsDescription' },
  { Icon: ClipboardCheck, titleKey: 'nextSteps.prepTitle', descriptionKey: 'nextSteps.prepDescription' },
  { Icon: GitCompare, titleKey: 'nextSteps.compareTitle', descriptionKey: 'nextSteps.compareDescription' },
] as const;

export function NextSteps({ onFinish }: NextStepsProps) {
  const { t } = useTranslation('onboarding');
  return (
    <div className="space-y-6">
      <div className="text-center space-y-2">
        <h1 className="font-serif text-2xl text-brand-slate-800">
          {t('nextSteps.heading')}
        </h1>
        <p className="text-sm text-brand-slate-500 leading-relaxed max-w-md mx-auto">
          {t('nextSteps.body')}
        </p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        {features.map(({ Icon, titleKey, descriptionKey }) => (
          <Card key={titleKey} className="flex items-start gap-3 p-4">
            <div className="bg-brand-teal-50 rounded-full p-2 shrink-0">
              <Icon
                className="text-brand-teal-500"
                size={20}
                strokeWidth={1.8}
                aria-hidden="true"
              />
            </div>
            <div>
              <p className="text-sm font-medium text-brand-slate-800">
                {t(titleKey)}
              </p>
              <p className="text-xs text-brand-slate-500 mt-0.5 leading-relaxed">
                {t(descriptionKey)}
              </p>
            </div>
          </Card>
        ))}
      </div>

      <div className="flex items-center justify-center gap-3 pt-2">
        <Link to="/iep-101">
          <Button variant="secondary" data-testid="onboarding-learn">{t('nextSteps.learnLink')}</Button>
        </Link>
        <Button onClick={onFinish} data-testid="onboarding-finish">{t('nextSteps.finish')}</Button>
      </div>
    </div>
  );
}
