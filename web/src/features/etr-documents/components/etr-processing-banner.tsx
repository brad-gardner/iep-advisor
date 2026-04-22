import { Notice } from '@/components/ui/notice';

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
          <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-brand-amber-500" />
          <span>{copy}</span>
        </div>
      </Notice>
    </div>
  );
}
