import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { getAuthoredPdfDownloadUrl } from '../api/documents-api';
import { useAuthoredPdfStatus } from '../hooks/use-authored-pdf-status';

interface AuthoredPdfDownloadProps {
  versionId: number;
  initialStatus?: string | null;
  // Retry is educator-only; parents (future) would see the error without retry.
  canRetry?: boolean;
  // Compact mode omits the framed error Notice for inline table cells.
  compact?: boolean;
}

// Renders the PDF download action plus its live render status for a finalized
// authored version. Polls while Pending via useAuthoredPdfStatus. Rendered →
// download link; Error → message (+ Retry if allowed); Pending → generating hint.
// Mirrors iep-versions/download-pdf-button for the authored-document surface.
export function AuthoredPdfDownload({
  versionId,
  initialStatus,
  canRetry = false,
  compact = false,
}: AuthoredPdfDownloadProps) {
  const { status, errorMessage, isLoading, timedOut, retry, isRetrying, refresh } =
    useAuthoredPdfStatus(versionId, initialStatus);
  const [isDownloading, setIsDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState(false);

  // Fetch a fresh SAS URL on click (this call records the FERPA Export audit — a
  // status poll deliberately does not), then open it in a new tab.
  const download = async () => {
    setIsDownloading(true);
    setDownloadError(false);
    try {
      const res = await getAuthoredPdfDownloadUrl(versionId);
      if (res.success && res.data?.url) {
        window.open(res.data.url, '_blank', 'noopener,noreferrer');
      } else {
        setDownloadError(true);
      }
    } catch {
      setDownloadError(true);
    } finally {
      setIsDownloading(false);
    }
  };

  if (isLoading && status === null) {
    return (
      <span className="text-sm text-brand-slate-500" role="status" aria-live="polite">
        Checking PDF…
      </span>
    );
  }

  if (status === 'Rendered') {
    return (
      <div className={compact ? 'flex items-center gap-2' : 'space-y-2'}>
        <Button
          variant="secondary"
          size={compact ? 'sm' : 'md'}
          onClick={() => void download()}
          disabled={isDownloading}
          data-testid={`download-authored-pdf-${versionId}`}
        >
          {isDownloading ? 'Preparing…' : 'Download PDF'}
        </Button>
        {downloadError && (
          <span className="text-sm text-brand-danger-700" role="status" aria-live="polite">
            Couldn’t prepare the download. Please try again.
          </span>
        )}
      </div>
    );
  }

  if (status === 'Error') {
    if (compact) {
      return (
        <div className="flex items-center gap-2" role="status" aria-live="polite">
          <span className="text-sm text-brand-danger-700">PDF failed</span>
          {canRetry && (
            <Button
              variant="secondary"
              size="sm"
              onClick={() => void retry()}
              disabled={isRetrying}
              data-testid={`retry-authored-pdf-${versionId}`}
            >
              {isRetrying ? 'Retrying…' : 'Retry'}
            </Button>
          )}
        </div>
      );
    }
    return (
      <div className="space-y-2">
        <Notice variant="error" title="PDF could not be generated">
          {errorMessage || 'The PDF render failed.'}
        </Notice>
        {canRetry && (
          <Button
            variant="secondary"
            onClick={() => void retry()}
            disabled={isRetrying}
            data-testid={`retry-authored-pdf-${versionId}`}
          >
            {isRetrying ? 'Retrying…' : 'Retry'}
          </Button>
        )}
      </div>
    );
  }

  // Pending (or unknown): show generating. Once polling caps out (timedOut), the
  // status no longer auto-refreshes, so offer a manual "Check again".
  return (
    <div
      className="flex items-center gap-2"
      role="status"
      aria-live="polite"
      data-testid={`authored-pdf-generating-${versionId}`}
    >
      <span className="text-sm text-brand-slate-500">
        {timedOut ? 'Still generating — check again shortly.' : 'Generating PDF…'}
      </span>
      {timedOut && (
        <Button
          variant="secondary"
          size="sm"
          onClick={() => void refresh()}
          data-testid={`check-authored-pdf-${versionId}`}
        >
          Check again
        </Button>
      )}
    </div>
  );
}
