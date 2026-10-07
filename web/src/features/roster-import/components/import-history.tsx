import { useTranslation } from 'react-i18next';
import { FileSpreadsheet } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/empty-state';
import { Table, type TableColumn } from '@/components/ui/table';
import { formatDate } from '@/lib/format-date';
import type { ImportBatch, ImportBatchStatus } from '../types';
import { countsSummary } from '../lib/import-file';

interface ImportHistoryProps {
  batches: ImportBatch[];
  loading: boolean;
}

const STATUS_BADGE: Record<ImportBatchStatus, 'success' | 'warning' | 'neutral'> = {
  Committed: 'success',
  Previewed: 'warning',
  Committing: 'warning',
  Discarded: 'neutral',
};

// Batches the district has previewed/committed, newest first (server order).
export function ImportHistory({ batches, loading }: ImportHistoryProps) {
  const { t } = useTranslation('roster-import');
  const columns: TableColumn<ImportBatch>[] = [
    { key: 'file', header: t('history.columns.file'), cell: (b) => b.fileName, sortValue: (b) => b.fileName },
    {
      key: 'kind',
      header: t('history.columns.kind'),
      hideBelow: 'md',
      cell: (b) => t(`history.kind.${b.kind}`),
      sortValue: (b) => b.kind,
    },
    {
      key: 'status',
      header: t('history.columns.status'),
      cell: (b) => <Badge variant={STATUS_BADGE[b.status]}>{t(`history.status.${b.status}`)}</Badge>,
      sortValue: (b) => b.status,
    },
    { key: 'counts', header: t('history.columns.rows'), hideBelow: 'md', cell: (b) => countsSummary(b.counts) },
    {
      key: 'date',
      header: t('history.columns.date'),
      align: 'right',
      cell: (b) => formatDate(b.committedAt ?? b.createdAt, ''),
      sortValue: (b) => b.committedAt ?? b.createdAt,
    },
    {
      key: 'by',
      header: t('history.columns.by'),
      hideBelow: 'lg',
      cell: (b) => b.createdByName,
      sortValue: (b) => b.createdByName,
    },
  ];

  return (
    <section className="space-y-3" data-testid="import-history">
      <h2 className="font-serif text-lg">{t('history.heading')}</h2>
      <Table
        label={t('history.tableLabel')}
        data-testid="import-history-list"
        columns={columns}
        rows={batches}
        rowKey={(b) => b.batchId}
        defaultSort={{ key: 'date', direction: 'desc' }}
        loading={loading}
        empty={
          <EmptyState
            data-testid="import-history-empty"
            icon={FileSpreadsheet}
            title={t('history.emptyTitle')}
            description={t('history.emptyDescription')}
          />
        }
      />
    </section>
  );
}
