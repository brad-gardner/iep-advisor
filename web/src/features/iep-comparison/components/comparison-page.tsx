import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ComparisonView } from './comparison-view';
import { usePageTitle } from '@/hooks/use-page-title';

export function ComparisonPage() {
  const { t } = useTranslation('iep-comparison');
  usePageTitle(t('pageTitle'));
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
