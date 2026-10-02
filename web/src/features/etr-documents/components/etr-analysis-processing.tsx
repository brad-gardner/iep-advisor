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
        Analyzing Your ETR
      </h3>
      <p className="text-brand-slate-500 text-sm text-center max-w-md mb-6">
        This usually takes a few minutes. We're reviewing assessment
        completeness, eligibility determination, and identifying areas that
        may need attention.
      </p>
      <Button variant="ghost" onClick={onReload}>
        Check Status
      </Button>
    </div>
  );
}
