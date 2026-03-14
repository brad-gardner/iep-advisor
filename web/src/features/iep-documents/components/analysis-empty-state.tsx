import { Search } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface AnalysisEmptyStateProps {
  onTrigger: () => void;
  isTriggering: boolean;
}

export function AnalysisEmptyState({ onTrigger, isTriggering }: AnalysisEmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4">
      <div className="w-12 h-12 rounded-full bg-brand-teal-50 flex items-center justify-center mb-4">
        <Search className="w-6 h-6 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
      </div>
      <h3 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
        Analyze Your IEP
      </h3>
      <p className="text-brand-slate-400 text-sm text-center max-w-md mb-6">
        Get a comprehensive analysis of your child's IEP, including plain-language
        explanations, goal evaluations, potential concerns, and suggested questions
        for your next IEP meeting.
      </p>
      <Button onClick={onTrigger} disabled={isTriggering}>
        {isTriggering ? 'Starting Analysis...' : 'Analyze IEP'}
      </Button>
    </div>
  );
}
