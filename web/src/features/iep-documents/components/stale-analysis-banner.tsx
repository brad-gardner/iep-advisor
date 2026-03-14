import { useState } from 'react';

interface StaleAnalysisBannerProps {
  onReanalyze: () => void;
  isReanalyzing: boolean;
}

export function StaleAnalysisBanner({ onReanalyze, isReanalyzing }: StaleAnalysisBannerProps) {
  const [dismissed, setDismissed] = useState(false);

  if (dismissed) return null;

  return (
    <div className="bg-amber-50 border border-amber-200 rounded-lg p-4 flex items-center justify-between gap-4">
      <div className="flex items-center gap-3">
        <svg className="w-5 h-5 text-amber-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z" />
        </svg>
        <p className="text-sm text-amber-800">
          Your advocacy goals have changed since this analysis was run. Re-analyze to check
          alignment with your updated priorities.
        </p>
      </div>
      <div className="flex gap-2 shrink-0">
        <button
          onClick={() => setDismissed(true)}
          className="text-xs text-amber-600 hover:text-amber-800 transition-colors"
        >
          Dismiss
        </button>
        <button
          onClick={onReanalyze}
          disabled={isReanalyzing}
          className="px-3 py-1.5 bg-amber-600 hover:bg-amber-700 disabled:opacity-50 rounded text-xs text-white transition-colors"
        >
          {isReanalyzing ? 'Re-analyzing...' : 'Re-analyze'}
        </button>
      </div>
    </div>
  );
}
