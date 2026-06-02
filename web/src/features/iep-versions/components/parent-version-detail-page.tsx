import { useParams } from 'react-router-dom';
import { IepVersionDetailPage } from './iep-version-detail-page';

// Parent context: read-only, no PDF retry; back link goes to the child overview.
export function ParentVersionDetailPage() {
  const { childId } = useParams<{ childId: string }>();
  return (
    <IepVersionDetailPage
      canRetry={false}
      backTo={`/children/${childId}/overview`}
      backLabel="Back to child"
    />
  );
}
