import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';

interface EtrAnalysisProcessingProps {
  onReload: () => void;
}

export function EtrAnalysisProcessing({ onReload }: EtrAnalysisProcessingProps) {
  const { t } = useTranslation('etr-documents');
  return (
    <div
      className="flex flex-col items-center justify-center py-16 px-4"
      data-testid="etr-analysis-processing"
    >
      <Spinner size="lg" label={t('processing.label')} className="mb-4" />
      <h3 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
        {t('processing.heading')}
      </h3>
      <p className="text-brand-slate-500 text-sm text-center max-w-md mb-6">
        {t('processing.body')}
      </p>
      <Button variant="ghost" onClick={onReload}>
        {t('processing.checkStatus')}
      </Button>
    </div>
  );
}
