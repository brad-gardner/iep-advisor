import { useState } from 'react';
import { Notice } from '@/components/ui/notice';
import { Button } from '@/components/ui/button';

interface StaleAnalysisBannerProps {
  onReanalyze: () => void;
  isReanalyzing: boolean;
}

export function StaleAnalysisBanner({ onReanalyze, isReanalyzing }: StaleAnalysisBannerProps) {
  const [dismissed, setDismissed] = useState(false);

  if (dismissed) return null;

  return (
    <div className="flex items-center justify-between gap-4">
      <div className="flex-1">
        <Notice variant="warning" title="Analysis may be outdated">
          Your advocacy goals have changed since this analysis was run. Re-analyze to check
          alignment with your updated priorities.
        </Notice>
      </div>
      <div className="flex gap-2 shrink-0">
        <Button variant="ghost" onClick={() => setDismissed(true)} className="text-[11px]">
          Dismiss
        </Button>
        <Button variant="amber" onClick={onReanalyze} disabled={isReanalyzing}>
          {isReanalyzing ? 'Re-analyzing...' : 'Re-analyze'}
        </Button>
      </div>
    </div>
  );
}
