import { ClipboardCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';

interface MeetingPrepEmptyStateProps {
  onGenerate: () => void;
  isGenerating: boolean;
  contextLabel?: 'IEP' | 'ETR';
  // When the generate affordance is provided elsewhere (e.g. the standalone
  // tab's date control), suppress this empty state's own button.
  hideCta?: boolean;
}

export function MeetingPrepEmptyState({
  onGenerate,
  isGenerating,
  contextLabel = 'IEP',
  hideCta = false,
}: MeetingPrepEmptyStateProps) {
  const sourceSentence =
    contextLabel === 'ETR'
      ? "on your child's ETR."
      : "on your child's IEP.";
  return (
    <EmptyState
      icon={ClipboardCheck}
      title="Prepare for Your Meeting"
      description={`Generate a personalized meeting prep checklist with questions to ask, documents to bring, rights to reference, and potential red flags based ${sourceSentence}`}
      action={
        hideCta ? undefined : (
          <Button onClick={onGenerate} loading={isGenerating} data-testid="generate-meeting-prep">
            Generate Meeting Prep
          </Button>
        )
      }
    />
  );
}
