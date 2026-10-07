import { useTranslation } from 'react-i18next';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';

interface EtrProcessingBannerProps {
  status: 'uploaded' | 'processing' | string;
}

export function EtrProcessingBanner({ status }: EtrProcessingBannerProps) {
  const { t } = useTranslation('etr-documents');
  const title = status === 'uploaded' ? t('processingBanner.queuedTitle') : t('processingBanner.analyzingTitle');
  const copy = status === 'uploaded' ? t('processingBanner.queuedBody') : t('processingBanner.analyzingBody');

  return (
    <div data-testid="etr-processing-banner">
      <Notice variant="warning" title={title}>
        <div className="flex items-center gap-2">
          <Spinner size="sm" tone="current" label={t('processingBanner.processingLabel')} className="text-brand-amber-500" />
          <span>{copy}</span>
        </div>
      </Notice>
    </div>
  );
}
