import { useState } from 'react';
import { useTranslation } from 'react-i18next';
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
import { usePageTitle } from '@/hooks/use-page-title';
import { relativeTime } from '@/lib/relative-time';
import { deleteDocument } from '../api/documents-api';
import { useDocumentList } from '../hooks/use-document-list';
import { useAuthoredVersions } from '../hooks/use-authored-versions';
import { NewDocumentModal } from '../components/new-document-modal';
import { AuthoredPdfDownload } from '../components/authored-pdf-download';
import { SignatureStatusBadge } from '../components/signature-status-badge';
import { documentStatusLabel } from '../lib/document-status-label';
import type {
  AuthoredDocumentVersionSummaryDto,
  DocumentInstanceStatus,
  DocumentInstanceSummaryDto,
} from '../types';

const statusVariant: Record<DocumentInstanceStatus, 'neutral' | 'warning' | 'success'> = {
  Draft: 'neutral',
  Finalizing: 'warning',
  Finalized: 'success',
};

export function DocumentListPage() {
  const { t } = useTranslation('document-authoring');
  usePageTitle(t('list.title'));
  const { studentId: studentIdParam } = useParams<{ studentId: string }>();
  const studentId = Number(studentIdParam);
  const navigate = useNavigate();
  const { show } = useToast();
  const { documents, isLoading, error, removeDocument } = useDocumentList(studentId);
  const {
    versions,
    isLoading: versionsLoading,
    error: versionsError,
  } = useAuthoredVersions(studentId);

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
        show({ message: t('list.documentDeletedToast'), variant: 'success' });
      } else {
        setDeleteError(res.message ?? t('list.deleteGenericError'));
      }
    } catch {
      setDeleteError(t('list.deleteGenericError'));
    } finally {
      setDeleting(false);
    }
  };

  const newButton = (testId: string) => (
    <Button onClick={() => setIsNewOpen(true)} data-testid={testId}>
      <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
      {t('list.newDocument')}
    </Button>
  );

  const columns: TableColumn<DocumentInstanceSummaryDto>[] = [
    {
      key: 'type',
      header: t('list.columnDocument'),
      cell: (d) => d.documentTypeDisplayName,
      sortValue: (d) => d.documentTypeDisplayName,
    },
    {
      key: 'status',
      header: t('list.columnStatus'),
      cell: (d) => <Badge variant={statusVariant[d.status]}>{documentStatusLabel(d.status)}</Badge>,
      sortValue: (d) => d.status,
    },
    {
      key: 'version',
      header: t('list.columnTemplateVersion'),
      cell: (d) => `v${d.templateVersionNumber}`,
      align: 'right',
      hideBelow: 'md',
    },
    {
      key: 'edited',
      header: t('list.columnLastEdited'),
      cell: (d) => (d.lastEditedAt ? relativeTime(d.lastEditedAt) : '—'),
      sortValue: (d) => d.lastEditedAt ?? '',
      hideBelow: 'md',
    },
  ];

  const versionColumns: TableColumn<AuthoredDocumentVersionSummaryDto>[] = [
    {
      key: 'type',
      header: t('list.columnDocument'),
      cell: (v) => v.documentTypeDisplayName,
      sortValue: (v) => v.documentTypeDisplayName,
    },
    {
      key: 'version',
      header: t('list.columnVersion'),
      cell: (v) => `v${v.versionNumber}`,
      align: 'right',
    },
    {
      key: 'finalized',
      header: t('list.columnFinalized'),
      cell: (v) => relativeTime(v.finalizedAt),
      sortValue: (v) => v.finalizedAt,
      hideBelow: 'md',
    },
    {
      key: 'amendment',
      header: t('list.columnAmendment'),
      cell: (v) =>
        v.amendsVersionId != null ? (
          <span className="text-xs text-brand-slate-500">
            {t('list.amendsVersion', { number: v.amendsVersionNumber ?? v.amendsVersionId })}
          </span>
        ) : (
          '—'
        ),
      hideBelow: 'lg',
    },
    {
      key: 'signature',
      header: t('list.columnSignature'),
      cell: (v) => <SignatureStatusBadge status={v.signatureStatus} data-testid={`version-signature-${v.id}`} />,
    },
    {
      key: 'pdf',
      header: t('list.columnPdf'),
      cell: (v) => (
        <AuthoredPdfDownload
          versionId={v.id}
          initialStatus={v.pdfRenderStatus}
          canRetry
          compact
        />
      ),
      align: 'right',
    },
  ];

  return (
    <PageLayout
      title={t('list.title')}
      breadcrumb={[
        { label: t('list.breadcrumbStudent'), to: `/educator/students/${studentId}` },
        { label: t('list.breadcrumbDocuments') },
      ]}
      actions={newButton('new-document')}
    >
      {error && (
        <Notice variant="error" title={t('list.loadErrorTitle')}>
          {error.kind === 'server' ? error.message : t('list.loadErrorGeneric')}
        </Notice>
      )}

      {!error && !isLoading && documents.length === 0 ? (
        <EmptyState
          icon={FileText}
          title={t('list.noDocumentsTitle')}
          description={t('list.noDocumentsDescription')}
          action={newButton('new-document-empty')}
          data-testid="empty-hint"
        />
      ) : (
        <Table
          label={t('list.breadcrumbDocuments')}
          columns={columns}
          rows={documents}
          rowKey={(d) => d.id}
          loading={isLoading}
          rowHref={(d) => `/educator/documents/${d.id}`}
          rowActions={(d) => [
            {
              label: t('list.deleteAction'),
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

      {versionsError ? (
        <Notice variant="error" title={t('list.finalizedVersionsLoadErrorTitle')}>
          {versionsError.kind === 'server' ? versionsError.message : t('list.finalizedVersionsLoadErrorGeneric')}
        </Notice>
      ) : (
        (versionsLoading || versions.length > 0) && (
          <div className="mt-8">
            <h2 className="mb-3 font-serif text-lg text-brand-slate-800">{t('list.finalizedVersionsHeading')}</h2>
            <Table
              label={t('list.finalizedVersionsHeading')}
              columns={versionColumns}
              rows={versions}
              rowKey={(v) => v.id}
              loading={versionsLoading}
              rowHref={(v) => `/educator/students/${studentId}/authored-versions/${v.id}`}
              data-testid="authored-versions-table"
            />
          </div>
        )
      )}

      <NewDocumentModal
        studentId={studentId}
        open={isNewOpen}
        onClose={() => setIsNewOpen(false)}
        onCreated={(id) => {
          setIsNewOpen(false);
          show({ message: t('list.documentCreatedToast'), variant: 'success' });
          openEditor(id);
        }}
      />

      <ConfirmDialog
        open={deleteTarget !== null}
        title={t('list.deleteDialogTitle')}
        message={t('list.deleteDialogMessage', {
          documentType: deleteTarget?.documentTypeDisplayName ?? t('list.documentFallback'),
        })}
        confirmLabel={t('list.deleteDialogConfirm')}
        loading={deleting}
        error={deleteError}
        onConfirm={confirmDelete}
        onCancel={() => setDeleteTarget(null)}
        data-testid="document-delete-dialog"
      />
    </PageLayout>
  );
}
