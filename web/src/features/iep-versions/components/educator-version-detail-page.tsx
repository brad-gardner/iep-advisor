import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { IepVersionDetailPage } from './iep-version-detail-page';

// Educator context: PDF retry is allowed; back link goes to the student.
export function EducatorVersionDetailPage() {
  const { t } = useTranslation('iep-versions');
  const { studentId } = useParams<{ studentId: string }>();
  return (
    <IepVersionDetailPage
      canRetry
      backTo={`/educator/students/${studentId}`}
      backLabel={t('detailPage.backToStudent')}
    />
  );
}
