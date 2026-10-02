import { useMeetingPrep } from '@/features/meeting-prep/hooks/use-meeting-prep';
import { MeetingPrepTab } from '@/features/meeting-prep/components/meeting-prep-tab';
import { useEtrAnalysis } from '../hooks/use-etr-analysis';

interface EtrMeetingPrepTabProps {
  etrId: number;
  childProfileId: number;
}

export function EtrMeetingPrepTab({
  etrId,
  childProfileId,
}: EtrMeetingPrepTabProps) {
  const { run, source } = useEtrAnalysis(childProfileId, etrId);
  const analysisCreatedAt = source?.status === 'Completed' ? run?.createdAt ?? null : null;

  const {
    checklist,
    isLoading,
    isGenerating,
    generateFromEtr,
    reload,
  } = useMeetingPrep(childProfileId, undefined, { type: 'etr', id: etrId });

  return (
    <MeetingPrepTab
      checklist={checklist}
      isLoading={isLoading}
      isGenerating={isGenerating}
      onGenerate={() => generateFromEtr(etrId)}
      onReload={reload}
      analysisCreatedAt={analysisCreatedAt}
      contextLabel="ETR"
    />
  );
}
