import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { IepVersionDetailPage } from './iep-version-detail-page';

// Parent context: read-only, no PDF retry; back link goes to the child overview.
export function ParentVersionDetailPage() {
  const { t } = useTranslation('iep-versions');
  const { childId } = useParams<{ childId: string }>();
  return (
    <IepVersionDetailPage
      canRetry={false}
      backTo={`/children/${childId}/overview`}
      backLabel={t('detailPage.backToChild')}
    />
  );
}
