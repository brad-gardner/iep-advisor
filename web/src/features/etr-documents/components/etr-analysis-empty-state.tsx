import { Search } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface EtrAnalysisEmptyStateProps {
  onStart: () => void;
  isTriggering: boolean;
}

export function EtrAnalysisEmptyState({ onStart, isTriggering }: EtrAnalysisEmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4">
      <div className="w-12 h-12 rounded-full bg-brand-teal-50 flex items-center justify-center mb-4">
        <Search className="w-6 h-6 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
      </div>
      <h3 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
        Analyze this ETR
      </h3>
      <p className="text-brand-slate-400 text-sm text-center max-w-md mb-6">
        We'll review assessment completeness, eligibility determination, red flags,
        and generate suggested questions for your ETR meeting.
      </p>
      <Button onClick={onStart} disabled={isTriggering} data-testid="etr-analyze-button">
        {isTriggering ? 'Starting Analysis...' : 'Run analysis'}
      </Button>
    </div>
  );
}
