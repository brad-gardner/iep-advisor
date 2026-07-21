import { useState } from 'react';
import { FileText, Plus } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Table, type TableColumn } from '@/components/ui/table';
import { useToast } from '@/components/ui/toast';
import { relativeTime } from '@/features/iep-authoring/lib/relative-time';
import { deleteDocument } from '../api/documents-api';
import { useDocumentList } from '../hooks/use-document-list';
import { NewDocumentModal } from '../components/new-document-modal';
import type { DocumentInstanceStatus, DocumentInstanceSummaryDto } from '../types';

const statusVariant: Record<DocumentInstanceStatus, 'neutral' | 'warning' | 'success'> = {
  Draft: 'neutral',
  Finalizing: 'warning',
  Finalized: 'success',
};

export function DocumentListPage() {
  const { studentId: studentIdParam } = useParams<{ studentId: string }>();
  const studentId = Number(studentIdParam);
  const navigate = useNavigate();
  const { show } = useToast();
  const { documents, isLoading, error, removeDocument } = useDocumentList(studentId);

  const [isNewOpen, setIsNewOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<DocumentInstanceSummaryDto | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const openEditor = (instanceId: number) => navigate(`/educator/documents/${instanceId}`);

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    setDeleteError(null);
    try {
      const res = await deleteDocument(deleteTarget.id);
      if (res.success) {
        removeDocument(deleteTarget.id);
        setDeleteTarget(null);
        show({ message: 'Document deleted', variant: 'success' });
      } else {
        setDeleteError(res.message ?? 'Could not delete the document.');
      }
    } catch {
      setDeleteError('Could not delete the document.');
    } finally {
      setDeleting(false);
    }
  };

  const newButton = (testId: string) => (
    <Button onClick={() => setIsNewOpen(true)} data-testid={testId}>
      <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
      New document
    </Button>
  );

  const columns: TableColumn<DocumentInstanceSummaryDto>[] = [
    {
      key: 'type',
      header: 'Document',
      cell: (d) => d.documentTypeDisplayName,
      sortValue: (d) => d.documentTypeDisplayName,
    },
    {
      key: 'status',
      header: 'Status',
      cell: (d) => <Badge variant={statusVariant[d.status]}>{d.status}</Badge>,
      sortValue: (d) => d.status,
    },
    {
      key: 'version',
      header: 'Template v',
      cell: (d) => `v${d.templateVersionNumber}`,
      align: 'right',
      hideBelow: 'md',
    },
    {
      key: 'edited',
      header: 'Last edited',
      cell: (d) => (d.lastEditedAt ? relativeTime(d.lastEditedAt) : '—'),
      sortValue: (d) => d.lastEditedAt ?? '',
      hideBelow: 'md',
    },
  ];

  return (
    <PageLayout
      title="Documents"
      breadcrumb={[
        { label: 'Student', to: `/educator/students/${studentId}` },
        { label: 'Documents' },
      ]}
      actions={newButton('new-document')}
    >
      {error && (
        <Notice variant="error" title="Could not load documents">
          {error}
        </Notice>
      )}

      {!error && !isLoading && documents.length === 0 ? (
        <EmptyState
          icon={FileText}
          title="No documents yet"
          description="Create an IEP, ETR, or other document from a template for this student."
          action={newButton('new-document-empty')}
          data-testid="empty-hint"
        />
      ) : (
        <Table
          label="Student documents"
          columns={columns}
          rows={documents}
          rowKey={(d) => d.id}
          loading={isLoading}
          rowHref={(d) => `/educator/documents/${d.id}`}
          rowActions={(d) => [
            {
              label: 'Delete',
              variant: 'danger',
              onSelect: () => {
                setDeleteError(null);
                setDeleteTarget(d);
              },
            },
          ]}
          rowActionLabel={(d) => d.documentTypeDisplayName}
          data-testid="documents-table"
        />
      )}

      <NewDocumentModal
        studentId={studentId}
        open={isNewOpen}
        onClose={() => setIsNewOpen(false)}
        onCreated={(id) => {
          setIsNewOpen(false);
          show({ message: 'Document created', variant: 'success' });
          openEditor(id);
        }}
      />

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete document"
        message={`This permanently deletes this ${deleteTarget?.documentTypeDisplayName ?? 'document'} draft. This cannot be undone.`}
        confirmLabel="Delete document"
        loading={deleting}
        error={deleteError}
        onConfirm={confirmDelete}
        onCancel={() => setDeleteTarget(null)}
        data-testid="document-delete-dialog"
      />
    </PageLayout>
  );
}
