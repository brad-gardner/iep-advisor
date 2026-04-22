import { Button } from '@/components/ui/button';

interface EtrAnalysisProcessingProps {
  onReload: () => void;
}

export function EtrAnalysisProcessing({ onReload }: EtrAnalysisProcessingProps) {
  return (
    <div
      className="flex flex-col items-center justify-center py-16 px-4"
      data-testid="etr-analysis-processing"
    >
      <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-brand-teal-500 mb-4" />
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
