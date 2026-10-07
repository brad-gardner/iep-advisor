import { useTranslation } from 'react-i18next';
import { ShieldCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Table, type TableColumn } from '@/components/ui/table';
import { usePageTitle } from '@/hooks/use-page-title';
import { getActiveLanguage } from '@/lib/i18n/format';
import { useAuditIntegrity } from '../hooks/use-audit-integrity';
import type { AuditIntegrityRunDto, AuditIntegrityStatus } from '../types';

const STATUS_VARIANT: Record<AuditIntegrityStatus, 'success' | 'error' | 'warning'> = {
  Ok: 'success',
  Broken: 'error',
  Failed: 'error',
};

/** Full date+time (not just date), formatted in the active i18next language
 *  — same local-helper shape as `features/analysis/components/run-history-
 *  list.tsx`'s own `formatDateTime`; `lib/format-date.ts`'s `formatDate` only
 *  covers a date, not a timestamp. */
function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(getActiveLanguage());
}

/** Platform admin `/admin/audit`: the audit-log hash-chain integrity history
 *  (pilot-gates plan, phase 1, decision 1), plus a button to run the check
 *  synchronously instead of waiting for the nightly worker. */
export function AdminAuditPage() {
  const { t } = useTranslation(['admin', 'common']);
  usePageTitle(t('audit.pageTitle'));
  const { runs, isLoading, error, reload, runCheck, isRunning, runError } = useAuditIntegrity();

  const columns: TableColumn<AuditIntegrityRunDto>[] = [
    {
      key: 'started',
      header: t('audit.column.started'),
      cell: (r) => formatDateTime(r.startedAt),
      sortValue: (r) => r.startedAt,
    },
    {
      key: 'completed',
      header: t('audit.column.completed'),
      cell: (r) => (r.completedAt ? formatDateTime(r.completedAt) : '—'),
    },
    { key: 'rowsChecked', header: t('audit.column.rowsChecked'), align: 'right', cell: (r) => r.rowsChecked },
    {
      key: 'status',
      header: t('common.column.status'),
      cell: (r) => (
        <Badge variant={STATUS_VARIANT[r.status]} data-testid={`audit-run-status-${r.id}`}>
          {t(`audit.status.${r.status}`)}
        </Badge>
      ),
      sortValue: (r) => r.status,
    },
    {
      key: 'firstBroken',
      header: t('audit.column.firstBrokenId'),
      align: 'right',
      cell: (r) => r.firstBrokenId ?? '—',
    },
    {
      key: 'detail',
      header: t('audit.column.detail'),
      cell: (r) => r.detail ?? '—',
    },
  ];

  return (
    <PageLayout
      title={t('audit.pageTitle')}
      subtitle={t('audit.subtitle')}
      actions={
        <Button onClick={runCheck} loading={isRunning} data-testid="run-audit-check">
          {t('audit.runCheckNow')}
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
            {t('common:ui.tryAgain')}
          </Button>
        </Notice>
      ) : (
        <Table
          label={t('audit.tableLabel')}
          data-testid="audit-integrity-table"
          columns={columns}
          rows={runs}
          rowKey={(r) => r.id}
          loading={isLoading}
          defaultSort={{ key: 'started', direction: 'desc' }}
          empty={
            <EmptyState
              icon={ShieldCheck}
              title={t('audit.emptyTitle')}
              description={t('audit.emptyDescription')}
            />
          }
        />
      )}
    </PageLayout>
  );
}
