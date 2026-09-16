import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Table, type TableColumn } from '@/components/ui/table';
import { usePageTitle } from '@/hooks/use-page-title';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { getExportDownloadUrl } from '../api/exports-api';
import { useDistrictExports } from '../hooks/use-district-exports';
import { EXPORT_JOB_STATUS_LABELS } from '../types';
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
  usePageTitle('Exports');
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
        setDownloadError(res.message ?? 'Could not prepare the download.');
      }
    } catch (err) {
      setDownloadError(apiErrorMessage(err, 'Could not prepare the download.'));
    } finally {
      setDownloadingId(null);
    }
  };

  const columns: TableColumn<ExportJobDto>[] = [
    {
      key: 'requested',
      header: 'Requested',
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
      header: 'Scope',
      cell: (j) => (j.scope === 'Student' ? j.studentName ?? 'Student' : 'District'),
      hideBelow: 'md',
    },
    {
      key: 'status',
      header: 'Status',
      cell: (j) => (
        <div>
          <Badge variant={STATUS_VARIANT[j.status]} data-testid={`export-status-${j.id}`}>
            {EXPORT_JOB_STATUS_LABELS[j.status]}
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
      header: 'Size',
      cell: (j) => formatFileSize(j.sizeBytes),
      align: 'right',
      hideBelow: 'md',
    },
    {
      key: 'counts',
      header: 'Students / files',
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
            Download
          </Button>
        ) : null,
      align: 'right',
    },
  ];

  return (
    <PageLayout
      title="Exports"
      breadcrumb={[{ label: 'Exports' }]}
      actions={
        <Button onClick={requestExport} loading={isRequesting} data-testid="request-district-export">
          Request district export
        </Button>
      }
    >
      {requestError && (
        <div role="alert" className="mb-4">
          <Notice variant="error" title={requestError} />
        </div>
      )}
      {downloadError && (
        <div role="alert" className="mb-4">
          <Notice variant="error" title={downloadError} />
        </div>
      )}

      {error ? (
        <Notice variant="error" title="Could not load export jobs">
          {error}
          <div>
            <Button size="sm" variant="secondary" className="mt-2" onClick={retry} data-testid="exports-retry">
              Try again
            </Button>
          </div>
        </Notice>
      ) : (
        <Table
          label="Export jobs"
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
