import { Card } from '@/components/ui/card';
import { useChildVersions } from '../hooks/use-version-list';
import { VersionHistoryList } from './version-history-list';

interface SchoolIepsCardProps {
  childId: number;
}

// Parent-side card showing finalized IEP versions the school has shared.
// Renders nothing when there are no versions yet — avoids an empty card for
// parents whose child has no school link.
export function SchoolIepsCard({ childId }: SchoolIepsCardProps) {
  const { versions, isLoading } = useChildVersions(childId);

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
