import { ShieldCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Table, type TableColumn } from '@/components/ui/table';
import { usePageTitle } from '@/hooks/use-page-title';
import { useAuditIntegrity } from '../hooks/use-audit-integrity';
import type { AuditIntegrityRunDto, AuditIntegrityStatus } from '../types';

const STATUS_VARIANT: Record<AuditIntegrityStatus, 'success' | 'error' | 'warning'> = {
  Ok: 'success',
  Broken: 'error',
  Failed: 'error',
};

/** Platform admin `/admin/audit`: the audit-log hash-chain integrity history
 *  (pilot-gates plan, phase 1, decision 1), plus a button to run the check
 *  synchronously instead of waiting for the nightly worker. */
export function AdminAuditPage() {
  usePageTitle('Audit integrity');
  const { runs, isLoading, error, reload, runCheck, isRunning, runError } = useAuditIntegrity();

  const columns: TableColumn<AuditIntegrityRunDto>[] = [
    {
      key: 'started',
      header: 'Started',
      cell: (r) => new Date(r.startedAt).toLocaleString(),
      sortValue: (r) => r.startedAt,
    },
    {
      key: 'completed',
      header: 'Completed',
      cell: (r) => (r.completedAt ? new Date(r.completedAt).toLocaleString() : '—'),
    },
    { key: 'rowsChecked', header: 'Rows checked', align: 'right', cell: (r) => r.rowsChecked },
    {
      key: 'status',
      header: 'Status',
      cell: (r) => (
        <Badge variant={STATUS_VARIANT[r.status]} data-testid={`audit-run-status-${r.id}`}>
          {r.status}
        </Badge>
      ),
      sortValue: (r) => r.status,
    },
    {
      key: 'firstBroken',
      header: 'First broken id',
      align: 'right',
      cell: (r) => r.firstBrokenId ?? '—',
    },
    {
      key: 'detail',
      header: 'Detail',
      cell: (r) => r.detail ?? '—',
    },
  ];

  return (
    <PageLayout
      title="Audit integrity"
      subtitle="Hash-chain integrity runs over the access audit log."
      actions={
        <Button onClick={runCheck} loading={isRunning} data-testid="run-audit-check">
          Run check now
        </Button>
      }
    >
      {runError && (
        <div role="alert">
          <Notice variant="error" title={runError} />
        </div>
      )}

      {error ? (
        <Notice variant="error" title={error}>
          <Button variant="secondary" size="sm" onClick={reload} className="mt-3">
            Retry
          </Button>
        </Notice>
      ) : (
        <Table
          label="Integrity runs"
          data-testid="audit-integrity-table"
          columns={columns}
          rows={runs}
          rowKey={(r) => r.id}
          loading={isLoading}
          defaultSort={{ key: 'started', direction: 'desc' }}
          empty={
            <EmptyState
              icon={ShieldCheck}
              title="No integrity runs yet"
              description="Run a check now, or wait for the nightly worker."
            />
          }
        />
      )}
    </PageLayout>
  );
}
