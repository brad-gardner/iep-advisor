import { ClipboardCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface MeetingPrepEmptyStateProps {
  onGenerate: () => void;
  isGenerating: boolean;
}

export function MeetingPrepEmptyState({ onGenerate, isGenerating }: MeetingPrepEmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4">
      <div className="w-12 h-12 rounded-full bg-brand-teal-50 flex items-center justify-center mb-4">
        <ClipboardCheck className="w-6 h-6 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
      </div>
      <h3 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
        Prepare for Your Meeting
      </h3>
      <p className="text-brand-slate-400 text-sm text-center max-w-md mb-6">
        Generate a personalized meeting prep checklist with questions to ask,
        documents to bring, rights to reference, and potential red flags based
        on your child's IEP.
      </p>
      <Button onClick={onGenerate} disabled={isGenerating}>
        {isGenerating ? 'Generating...' : 'Generate Meeting Prep'}
      </Button>
    </div>
  );
}
