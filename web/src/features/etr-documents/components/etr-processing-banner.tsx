import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';

interface EtrProcessingBannerProps {
  status: 'uploaded' | 'processing' | string;
}

export function EtrProcessingBanner({ status }: EtrProcessingBannerProps) {
  const title = status === 'uploaded' ? 'Queued for analysis' : "We're analyzing your ETR...";
  const copy =
    status === 'uploaded'
      ? 'Your document has been uploaded and will be processed shortly.'
      : 'Extracting sections and structuring content. This usually takes under two minutes.';

  return (
    <div data-testid="etr-processing-banner">
      <Notice variant="warning" title={title}>
        <div className="flex items-center gap-2">
          <Spinner size="sm" tone="current" label="Processing…" className="text-brand-amber-500" />
          <span>{copy}</span>
        </div>
      </Notice>
    </div>
  );
}
