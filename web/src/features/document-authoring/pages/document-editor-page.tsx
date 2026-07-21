import { Link, useParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { Spinner } from '@/components/ui/spinner';
import { useDocumentInstance } from '../hooks/use-document-instance';
import { DocumentEditor } from '../components/document-editor';

export function DocumentEditorPage() {
  const { instanceId: instanceIdParam } = useParams<{ instanceId: string }>();
  const instanceId = Number(instanceIdParam);
  const instance = useDocumentInstance(instanceId);
  const { detail, isLoading, loadError } = instance;

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading document…" />
      </div>
    );
  }

  if (loadError || !detail) {
    return (
      <EmptyState
        title="Document not found"
        description={loadError ?? 'This document may have been removed, or you may not have access.'}
        action={
          <Link to="/educator/students">
            <Button variant="secondary">Back to students</Button>
          </Link>
        }
      />
    );
  }

  return (
    <div className="space-y-6" data-testid="document-editor-page">
      <div>
        <Link
          to={`/educator/students/${detail.schoolStudentId}/documents`}
          className="text-sm text-brand-teal-600 hover:underline"
        >
          ← Back to documents
        </Link>
      </div>
      <DocumentEditor detail={detail} instance={instance} />
    </div>
  );
}
