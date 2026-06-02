import { Card } from '@/components/ui/card';
import { useFeatureFlag } from '@/hooks/use-feature-flags';
import { useChildVersions } from '../hooks/use-version-list';
import { VersionHistoryList } from './version-history-list';

interface SchoolIepsCardProps {
  childId: number;
}

// Parent-side card showing finalized IEP versions the school has shared.
// Gated on the SchoolSide flag; renders nothing when off or when there are
// no versions yet (avoids an empty card for parents with no school link).
export function SchoolIepsCard({ childId }: SchoolIepsCardProps) {
  const schoolSideEnabled = useFeatureFlag('SchoolSide');
  const { versions, isLoading } = useChildVersions(childId, schoolSideEnabled);

  if (!schoolSideEnabled) return null;
  if (!isLoading && versions.length === 0) return null;

  return (
    <Card data-testid="school-ieps-section">
      <h2 className="font-serif mb-1">School IEPs</h2>
      <p className="text-sm text-brand-slate-400 mb-4">
        Finalized IEP versions shared by your child's school.
      </p>
      <VersionHistoryList
        versions={versions}
        isLoading={isLoading}
        linkBase={`/children/${childId}/iep-versions`}
      />
    </Card>
  );
}
