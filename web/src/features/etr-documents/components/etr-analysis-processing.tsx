import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';

interface EtrAnalysisProcessingProps {
  onReload: () => void;
}

export function EtrAnalysisProcessing({ onReload }: EtrAnalysisProcessingProps) {
  return (
    <div
      className="flex flex-col items-center justify-center py-16 px-4"
      data-testid="etr-analysis-processing"
    >
      <Spinner size="lg" label="Analyzing…" className="mb-4" />
      <h3 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
        Analyzing your ETR
      </h3>
      <p className="text-brand-slate-400 text-sm text-center max-w-md mb-6">
        This typically takes 30-90 seconds. We're reviewing assessment completeness,
        eligibility determination, red flags, and drafting meeting questions.
      </p>
      <Button variant="ghost" onClick={onReload}>
        Check Status
      </Button>
    </div>
  );
}
