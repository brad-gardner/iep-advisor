import { useState } from 'react';
import { FileText, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { EmptyState } from '@/components/ui/empty-state';
import { PageLayout } from '@/components/ui/page-layout';
import { Table, type TableColumn } from '@/components/ui/table';
import { useToast } from '@/components/ui/toast';
import { useTemplates } from './hooks/use-templates';
import { CreateTemplateModal } from './create-template-modal';
import type { DocumentTemplateDto } from './types';

export function TemplateListPage() {
  const { templates, isLoading, error, reload, create } = useTemplates();
  const { show: showToast } = useToast();
  const [showCreate, setShowCreate] = useState(false);

  const handleCreate = async (data: Parameters<typeof create>[0]) => {
    const result = await create(data);
    if (result.success) {
      showToast({ message: 'Template created.', variant: 'success' });
    }
    return result;
  };

  const columns: TableColumn<DocumentTemplateDto>[] = [
    {
      key: 'state',
      header: 'State',
      cell: (t) => t.stateCode ?? 'Default',
      sortValue: (t) => t.stateCode ?? '',
    },
    {
      key: 'documentType',
      header: 'Document Type',
      cell: (t) => t.documentTypeDisplayName,
      sortValue: (t) => t.documentTypeDisplayName,
    },
    {
      key: 'name',
      header: 'Name',
      cell: (t) => t.name,
      sortValue: (t) => t.name.toLowerCase(),
    },
    {
      key: 'version',
      header: 'Latest Version',
      hideBelow: 'md',
      cell: (t) => (t.latestVersion ? `v${t.latestVersion.versionNumber}` : '—'),
      sortValue: (t) => t.latestVersion?.versionNumber ?? 0,
    },
    {
      key: 'status',
      header: 'Status',
      cell: (t) =>
        t.latestVersion ? (
          <Badge variant={t.latestVersion.status === 'Published' ? 'success' : 'neutral'}>
            {t.latestVersion.status}
          </Badge>
        ) : (
          '—'
        ),
      sortValue: (t) => t.latestVersion?.status ?? '',
    },
  ];

  return (
    <PageLayout
      title="Document Templates"
      subtitle={`${templates.length} template${templates.length !== 1 ? 's' : ''}`}
      actions={
        <Button onClick={() => setShowCreate(true)} data-testid="create-template-button">
          <Plus size={14} strokeWidth={1.8} className="mr-1.5" aria-hidden="true" />
          Create Template
        </Button>
      }
    >
      {error && (
        <Notice variant="error" title={error}>
          <Button variant="secondary" size="sm" onClick={reload} className="mt-3">
            Retry
          </Button>
        </Notice>
      )}

      <Table
        label="Document templates"
        data-testid="admin-templates-table"
        columns={columns}
        rows={templates}
        rowKey={(t) => t.id}
        loading={isLoading}
        defaultSort={{ key: 'state', direction: 'asc' }}
        empty={<EmptyState icon={FileText} title="No templates yet." />}
      />

      {/* Mounted only while open so the document-types fetch defers until the
          admin opens the form and re-runs (retryable) on each open. */}
      {showCreate && (
        <CreateTemplateModal
          open={showCreate}
          onClose={() => setShowCreate(false)}
          onCreate={handleCreate}
        />
      )}
    </PageLayout>
  );
}
