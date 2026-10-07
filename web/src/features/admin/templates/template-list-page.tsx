import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { FileText, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { EmptyState } from '@/components/ui/empty-state';
import { PageLayout } from '@/components/ui/page-layout';
import { Table, type TableColumn } from '@/components/ui/table';
import { useToast } from '@/components/ui/toast';
import { usePageTitle } from '@/hooks/use-page-title';
import { useTemplates } from './hooks/use-templates';
import { CreateTemplateModal } from './create-template-modal';
import type { DocumentTemplateDto } from './types';

export function TemplateListPage() {
  const { t } = useTranslation(['admin', 'common']);
  usePageTitle(t('templates.list.pageTitle'));
  const { templates, isLoading, error, reload, create } = useTemplates();
  const { show: showToast } = useToast();
  const [showCreate, setShowCreate] = useState(false);

  const handleCreate = async (data: Parameters<typeof create>[0]) => {
    const result = await create(data);
    if (result.success) {
      showToast({ message: t('templates.list.createdToast'), variant: 'success' });
    }
    return result;
  };

  const columns: TableColumn<DocumentTemplateDto>[] = [
    {
      key: 'state',
      header: t('templates.list.column.state'),
      cell: (row) => row.stateCode ?? t('templates.list.column.stateDefault'),
      sortValue: (row) => row.stateCode ?? '',
    },
    {
      key: 'documentType',
      header: t('templates.list.column.documentType'),
      cell: (row) => row.documentTypeDisplayName,
      sortValue: (row) => row.documentTypeDisplayName,
    },
    {
      key: 'name',
      header: t('templates.list.column.name'),
      cell: (row) => row.name,
      sortValue: (row) => row.name.toLowerCase(),
    },
    {
      key: 'version',
      header: t('templates.list.column.latestVersion'),
      hideBelow: 'md',
      cell: (row) => (row.latestVersion ? `v${row.latestVersion.versionNumber}` : '—'),
      sortValue: (row) => row.latestVersion?.versionNumber ?? 0,
    },
    {
      key: 'status',
      header: t('templates.list.column.status'),
      cell: (row) =>
        row.latestVersion ? (
          <Badge variant={row.latestVersion.status === 'Published' ? 'success' : 'neutral'}>
            {t(`templates.versionStatus.${row.latestVersion.status}`)}
          </Badge>
        ) : (
          '—'
        ),
      sortValue: (row) => row.latestVersion?.status ?? '',
    },
  ];

  return (
    <PageLayout
      title={t('templates.list.pageTitle')}
      subtitle={t('templates.list.subtitle', { count: templates.length })}
      actions={
        <Button onClick={() => setShowCreate(true)} data-testid="create-template-button">
          <Plus size={14} strokeWidth={1.8} className="mr-1.5" aria-hidden="true" />
          {t('templates.list.createButton')}
        </Button>
      }
    >
      {error && (
        <Notice variant="error" title={error}>
          <Button variant="secondary" size="sm" onClick={reload} className="mt-3">
            {t('common:ui.tryAgain')}
          </Button>
        </Notice>
      )}

      <Table
        label={t('templates.list.tableLabel')}
        data-testid="admin-templates-table"
        columns={columns}
        rows={templates}
        rowKey={(row) => row.id}
        rowHref={(row) => `/admin/templates/${row.id}`}
        loading={isLoading}
        defaultSort={{ key: 'state', direction: 'asc' }}
        empty={<EmptyState icon={FileText} title={t('templates.list.emptyTitle')} />}
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
