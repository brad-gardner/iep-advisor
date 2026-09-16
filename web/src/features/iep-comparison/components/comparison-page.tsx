import { useParams } from 'react-router-dom';
import { ComparisonView } from './comparison-view';
import { usePageTitle } from '@/hooks/use-page-title';

export function ComparisonPage() {
  usePageTitle('Compare IEPs');
  const { childId, iepId, otherId } = useParams<{
    childId: string;
    iepId: string;
    otherId: string;
  }>();

  return (
    <ComparisonView
      childId={Number(childId)}
      iepId={Number(iepId)}
      otherId={Number(otherId)}
    />
  );
}
