import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { usePdfStatus } from '../hooks/use-pdf-status';

interface DownloadPdfButtonProps {
  versionId: number;
  initialStatus?: string | null;
  // Retry is educator-only; parents see the error without a retry action.
  canRetry?: boolean;
}

// Renders the PDF download action plus its live status. Polls while Pending via
// usePdfStatus. Rendered → download link; Error → message (+ Retry if allowed);
// Pending → generating hint.
export function DownloadPdfButton({
  versionId,
  initialStatus,
  canRetry = false,
}: DownloadPdfButtonProps) {
  const { status, url, errorMessage, isLoading, timedOut, retry, isRetrying } =
    usePdfStatus(versionId, initialStatus);

  if (isLoading && status === null) {
    return <span className="text-sm text-brand-slate-500">Checking PDF…</span>;
  }

  if (status === 'Rendered' && url) {
    return (
      <a href={url} target="_blank" rel="noopener noreferrer" data-testid="download-pdf">
        <Button variant="secondary">Download PDF</Button>
      </a>
    );
  }

  if (status === 'Error') {
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
            data-testid="retry-pdf"
          >
            {isRetrying ? 'Retrying…' : 'Retry'}
          </Button>
        )}
      </div>
    );
  }

  // Pending (or unknown): show generating, and a softer note past the cap.
  return (
    <span className="text-sm text-brand-slate-500" data-testid="pdf-generating">
      {timedOut ? 'Still generating — check back shortly.' : 'Generating PDF…'}
    </span>
  );
}
