import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RefreshCw } from 'lucide-react';
import { Notice } from '@/components/ui/notice';
import { Button } from '@/components/ui/button';
import { reprocess } from '../api/etr-documents-api';

interface EtrErrorBannerProps {
  etrId: number;
  onRetried: () => void;
}

export function EtrErrorBanner({ etrId, onRetried }: EtrErrorBannerProps) {
  const { t } = useTranslation('etr-documents');
  const [isRetrying, setIsRetrying] = useState(false);
  const [retryError, setRetryError] = useState<string | null>(null);

  const handleRetry = async () => {
    setIsRetrying(true);
    setRetryError(null);
    try {
      const response = await reprocess(etrId);
      if (response.success) {
        onRetried();
      } else {
        setRetryError(response.message || t('errorBanner.retryFailed'));
      }
    } catch {
      setRetryError(t('errorBanner.retryFailed'));
    } finally {
      setIsRetrying(false);
    }
  };

  return (
    <div data-testid="etr-error-banner">
      <Notice variant="error" title={t('errorBanner.title')}>
        <div className="space-y-2">
          <p>
            {t('errorBanner.body')}
          </p>
          {retryError && <p className="text-brand-danger-700">{retryError}</p>}
          <Button
            variant="secondary"
            onClick={handleRetry}
            loading={isRetrying}
            data-testid="etr-retry-processing"
          >
            <RefreshCw
              className="w-4 h-4 mr-1.5"
              strokeWidth={1.8}
              aria-hidden="true"
            />
            {t('errorBanner.retry')}
          </Button>
        </div>
      </Notice>
    </div>
  );
}
