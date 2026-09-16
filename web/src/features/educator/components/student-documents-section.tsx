import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { VersionHistoryList } from '@/features/iep-versions/components/version-history-list';
import { useStudentVersions } from '@/features/iep-versions/hooks/use-version-list';
import { StudentDocumentsSummary } from '@/features/document-authoring/components/student-documents-summary';

// Documents summary plus the legacy typed IEP versions (shown only for
// students who still have them).
export function StudentDocumentsSection({ studentId }: { studentId: number }) {
  const { versions, isLoading: versionsLoading } = useStudentVersions(studentId);

  return (
    <section className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <h2 className="font-serif text-lg">Documents</h2>
        <Link to={`/educator/students/${studentId}/documents`} data-testid="manage-documents">
          <Button variant="secondary" size="sm">
            Manage documents
          </Button>
        </Link>
      </div>
      <Card data-testid="documents-section">
        <StudentDocumentsSummary studentId={studentId} />
      </Card>
      {!versionsLoading && versions.length > 0 && (
        <details data-testid="legacy-iep-versions">
          <summary className="cursor-pointer text-sm text-brand-slate-500">
            Legacy IEP versions ({versions.length})
          </summary>
          <Card className="mt-2">
            <VersionHistoryList
              versions={versions}
              isLoading={versionsLoading}
              linkBase={`/educator/students/${studentId}/iep-versions`}
            />
          </Card>
        </details>
      )}
    </section>
  );
}
