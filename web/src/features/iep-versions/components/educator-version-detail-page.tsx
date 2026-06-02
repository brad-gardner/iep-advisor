import { useParams } from 'react-router-dom';
import { IepVersionDetailPage } from './iep-version-detail-page';

// Educator context: PDF retry is allowed; back link goes to the student.
export function EducatorVersionDetailPage() {
  const { studentId } = useParams<{ studentId: string }>();
  return (
    <IepVersionDetailPage
      canRetry
      backTo={`/educator/students/${studentId}`}
      backLabel="Back to student"
    />
  );
}
