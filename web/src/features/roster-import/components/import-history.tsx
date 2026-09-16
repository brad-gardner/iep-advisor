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

const columns: TableColumn<ImportBatch>[] = [
  { key: 'file', header: 'File', cell: (b) => b.fileName, sortValue: (b) => b.fileName },
  { key: 'kind', header: 'Kind', hideBelow: 'md', cell: (b) => b.kind, sortValue: (b) => b.kind },
  {
    key: 'status',
    header: 'Status',
    cell: (b) => <Badge variant={STATUS_BADGE[b.status]}>{b.status}</Badge>,
    sortValue: (b) => b.status,
  },
  { key: 'counts', header: 'Rows', hideBelow: 'md', cell: (b) => countsSummary(b.counts) },
  {
    key: 'date',
    header: 'Date',
    align: 'right',
    cell: (b) => formatDate(b.committedAt ?? b.createdAt, ''),
    sortValue: (b) => b.committedAt ?? b.createdAt,
  },
  { key: 'by', header: 'By', hideBelow: 'lg', cell: (b) => b.createdByName, sortValue: (b) => b.createdByName },
];

// Batches the district has previewed/committed, newest first (server order).
export function ImportHistory({ batches, loading }: ImportHistoryProps) {
  return (
    <section className="space-y-3" data-testid="import-history">
      <h2 className="font-serif text-lg">Previous imports</h2>
      <Table
        label="Previous imports"
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
            title="No imports yet"
            description="Committed and previewed workbooks will be listed here."
          />
        }
      />
    </section>
  );
}
