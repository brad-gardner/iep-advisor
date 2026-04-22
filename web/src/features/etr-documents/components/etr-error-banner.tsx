import { useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { Notice } from '@/components/ui/notice';
import { Button } from '@/components/ui/button';
import { reprocess } from '../api/etr-documents-api';

interface EtrErrorBannerProps {
  etrId: number;
  onRetried: () => void;
}

export function EtrErrorBanner({ etrId, onRetried }: EtrErrorBannerProps) {
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
        setRetryError(response.message || 'Failed to queue reprocessing');
      }
    } catch {
      setRetryError('Failed to queue reprocessing');
    } finally {
      setIsRetrying(false);
    }
  };

  return (
    <div data-testid="etr-error-banner">
      <Notice variant="error" title="Processing failed">
        <div className="space-y-2">
          <p>
            We couldn't finish analyzing your ETR. This can happen when the PDF is
            scanned or has unusual formatting. You can retry processing below.
          </p>
          {retryError && <p className="text-brand-red">{retryError}</p>}
          <Button
            variant="secondary"
            onClick={handleRetry}
            disabled={isRetrying}
            data-testid="etr-retry-processing"
          >
            <RefreshCw
              className={`w-4 h-4 mr-1.5 ${isRetrying ? 'animate-spin' : ''}`}
              strokeWidth={1.8}
              aria-hidden="true"
            />
            {isRetrying ? 'Retrying...' : 'Retry processing'}
          </Button>
        </div>
      </Notice>
    </div>
  );
}
