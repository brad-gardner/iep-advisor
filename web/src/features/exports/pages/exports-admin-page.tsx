import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Table, type TableColumn } from '@/components/ui/table';
import { usePageTitle } from '@/hooks/use-page-title';
import { apiErrorMessage, loadErrorText } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { getExportDownloadUrl } from '../api/exports-api';
import { useDistrictExports } from '../hooks/use-district-exports';
import type { ExportJobDto, ExportJobStatus } from '../types';

const STATUS_VARIANT: Record<ExportJobStatus, 'neutral' | 'warning' | 'success' | 'error'> = {
  Queued: 'neutral',
  Running: 'warning',
  Completed: 'success',
  Failed: 'error',
};

function formatFileSize(bytes: number | null): string {
  if (bytes == null) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** District admin `/educator/admin/exports`: request a whole-district export
 *  and track every export job (district- and student-scoped) to completion
 *  (plan 7, decision 8). Polls while any job is still in flight. */
export function ExportsAdminPage() {
  const { t } = useTranslation('exports');
  usePageTitle(t('page.title'));
  const { jobs, isLoading, error, retry, requestExport, isRequesting, requestError } = useDistrictExports();
  const [downloadingId, setDownloadingId] = useState<number | null>(null);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  const handleDownload = async (jobId: number) => {
    setDownloadingId(jobId);
    setDownloadError(null);
    try {
      const res = await getExportDownloadUrl(jobId);
      if (res.success && res.data?.url) {
        window.open(res.data.url, '_blank', 'noopener,noreferrer');
      } else {
        setDownloadError(res.message ?? t('page.couldNotPrepareDownload'));
      }
    } catch (err) {
      setDownloadError(apiErrorMessage(err, t('page.couldNotPrepareDownload')));
    } finally {
      setDownloadingId(null);
    }
  };

  const columns: TableColumn<ExportJobDto>[] = [
    {
      key: 'requested',
      header: t('page.columns.requested'),
      cell: (j) => (
        <span>
          {formatDate(j.requestedAt)}
          {j.requestedByName ? ` by ${j.requestedByName}` : ''}
        </span>
      ),
      sortValue: (j) => j.requestedAt,
    },
    {
      key: 'scope',
      header: t('page.columns.scope'),
      cell: (j) => (j.scope === 'Student' ? j.studentName ?? t('page.columns.scopeStudentFallback') : t('page.columns.scopeDistrict')),
      hideBelow: 'md',
    },
    {
      key: 'status',
      header: t('page.columns.status'),
      cell: (j) => (
        <div>
          <Badge variant={STATUS_VARIANT[j.status]} data-testid={`export-status-${j.id}`}>
            {t(`page.status.${j.status}`)}
          </Badge>
          {j.status === 'Failed' && j.error && (
            <p className="mt-1 text-xs text-brand-danger-700" data-testid={`export-error-${j.id}`}>
              {j.error}
            </p>
          )}
        </div>
      ),
    },
    {
      key: 'size',
      header: t('page.columns.size'),
      cell: (j) => formatFileSize(j.sizeBytes),
      align: 'right',
      hideBelow: 'md',
    },
    {
      key: 'counts',
      header: t('page.columns.counts'),
      cell: (j) => `${j.studentCount} / ${j.fileCount}`,
      align: 'right',
      hideBelow: 'lg',
    },
    {
      key: 'download',
      header: '',
      cell: (j) =>
        j.status === 'Completed' ? (
          <Button
            size="sm"
            variant="secondary"
            onClick={() => handleDownload(j.id)}
            loading={downloadingId === j.id}
            data-testid={`export-download-${j.id}`}
          >
            {t('page.columns.download')}
          </Button>
        ) : null,
      align: 'right',
    },
  ];

  return (
    <PageLayout
      title={t('page.title')}
      breadcrumb={[{ label: t('page.breadcrumb') }]}
      actions={
        <Button onClick={requestExport} loading={isRequesting} data-testid="request-district-export">
          {t('page.requestExport')}
        </Button>
      }
    >
      {requestError && (
        <div role="alert" className="mb-4">
          <Notice variant="error" title={loadErrorText(requestError, t('page.requestExportFailed')) ?? ''} />
        </div>
      )}
      {downloadError && (
        <div role="alert" className="mb-4">
          <Notice variant="error" title={downloadError} />
        </div>
      )}

      {error ? (
        <Notice variant="error" title={t('page.couldNotLoadTitle')}>
          {loadErrorText(error, t('page.couldNotLoadTitle'))}
          <div>
            <Button size="sm" variant="secondary" className="mt-2" onClick={retry} data-testid="exports-retry">
              {t('page.tryAgain')}
            </Button>
          </div>
        </Notice>
      ) : (
        <Table
          label={t('page.tableLabel')}
          columns={columns}
          rows={jobs}
          rowKey={(j) => j.id}
          loading={isLoading}
          defaultSort={{ key: 'requested', direction: 'desc' }}
          data-testid="exports-table"
        />
      )}
    </PageLayout>
  );
}
