import { Search } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';

interface EtrAnalysisEmptyStateProps {
  onTrigger: () => void;
  isTriggering: boolean;
}

export function EtrAnalysisEmptyState({ onTrigger, isTriggering }: EtrAnalysisEmptyStateProps) {
  const { t } = useTranslation('etr-documents');
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4">
      <div className="w-12 h-12 rounded-full bg-brand-teal-50 flex items-center justify-center mb-4">
        <Search className="w-6 h-6 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
      </div>
      <h3 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
        {t('emptyState.heading')}
      </h3>
      <p className="text-brand-slate-500 text-sm text-center max-w-md mb-6">
        {t('emptyState.body')}
      </p>
      <Button onClick={onTrigger} loading={isTriggering} data-testid="etr-analyze-button">
        {t('emptyState.analyze')}
      </Button>
    </div>
  );
}
