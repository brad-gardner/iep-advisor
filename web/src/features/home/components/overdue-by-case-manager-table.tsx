import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Table, type TableColumn } from '@/components/ui/table';
import { EmptyState } from '@/components/ui/empty-state';
import { CalendarCheck2 } from 'lucide-react';
import { ObligationStatusChip } from '@/features/obligations/components/obligation-status-chip';
import { formatDate } from '@/lib/format-date';
import i18n from '@/lib/i18n';
import { obligationKindLabel } from '@/lib/obligation-label';
import { HomeSection } from './home-section';
import type { CaseManagerRowDto } from '../types';

const BOARD_LINK = '/educator/admin/compliance';

// Sorted by STUDENT by default (never by staff) — no per-staff ranking
// anywhere on this table, only counts/rows a case manager can act on.
// Column `header`s are translated outside the component (a plain `t()`
// call via the shared `i18n` instance, not `useTranslation`'s `t`) because
// this array is a MODULE-level constant, built once rather than inside
// `OverdueByCaseManagerTable` — it only ever reads English at MODULE LOAD
// time otherwise. Each header is still read fresh on every render of the
// `<Table>` below (not memoized away), so a language switch updates them.
const columns: TableColumn<CaseManagerRowDto>[] = [
  {
    key: 'student',
    get header() {
      return i18n.t('home:overdueTable.columnStudent');
    },
    cell: (row) => row.studentName,
    sortValue: (row) => row.studentName,
  },
  {
    key: 'caseManager',
    get header() {
      return i18n.t('home:overdueTable.columnCaseManager');
    },
    cell: (row) => row.caseManagerName ?? i18n.t('home:overdueTable.unassigned'),
  },
  {
    key: 'kind',
    get header() {
      return i18n.t('home:overdueTable.columnType');
    },
    cell: (row) => obligationKindLabel(row.kind),
  },
  {
    key: 'dueDate',
    get header() {
      return i18n.t('home:overdueTable.columnDueDate');
    },
    cell: (row) => formatDate(row.dueDate),
    sortValue: (row) => row.dueDate ?? '',
  },
  {
    key: 'status',
    get header() {
      return i18n.t('home:overdueTable.columnStatus');
    },
    cell: (row) => <ObligationStatusChip status={row.status} />,
  },
];

interface OverdueByCaseManagerTableProps {
  rows: CaseManagerRowDto[];
  /** True row count before the server's 50-row cap; only shown as a "showing
   * N of total" note (with a link to the full board) when it exceeds `rows.length`. */
  total?: number | null;
}

/** SchoolAdmin/DistrictAdmin "Overdue / at-risk" table — deliberately sorted
 * by student, never by case manager, so it reads as work to do rather than a
 * staff scorecard. Capped at 50 rows server-side; the compliance board has
 * the full, filterable list. */
export function OverdueByCaseManagerTable({ rows, total }: OverdueByCaseManagerTableProps) {
  // `obligations` alongside `home`: the `kind` column's cell calls the
  // staff-only `obligationKindLabel` helper (`obligations:kind.*`), and this
  // hook call is what makes a language switch re-render the table once that
  // namespace's Spanish loads.
  const { t } = useTranslation(['home', 'obligations']);
  const truncated = total != null && total > rows.length;
  return (
    <HomeSection title={t('overdueTable.title')} data-testid="home-overdue-by-case-manager">
      <Table
        label={t('overdueTable.tableLabel')}
        data-testid="home-overdue-by-case-manager-table"
        columns={columns}
        rows={rows}
        rowKey={(row) => `${row.studentId}-${row.kind}`}
        rowHref={(row) => `/educator/students/${row.studentId}`}
        defaultSort={{ key: 'student', direction: 'asc' }}
        empty={
          <EmptyState
            icon={CalendarCheck2}
            title={t('overdueTable.emptyTitle')}
            description={t('overdueTable.emptyDescription')}
          />
        }
      />
      {truncated && (
        <p
          className="mt-3 text-sm text-brand-slate-500"
          data-testid="home-overdue-by-case-manager-more"
        >
          {t('overdueTable.showingOfTotal', { shown: rows.length, total })}{' '}
          <Link to={BOARD_LINK} className="text-brand-teal-600 hover:underline">
            {t('overdueTable.viewBoard')}
          </Link>
        </p>
      )}
    </HomeSection>
  );
}
