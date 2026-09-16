import { Link } from 'react-router-dom';
import { Table, type TableColumn } from '@/components/ui/table';
import { EmptyState } from '@/components/ui/empty-state';
import { CalendarCheck2 } from 'lucide-react';
import { ObligationStatusChip } from '@/features/obligations/components/obligation-status-chip';
import { OBLIGATION_KIND_LABELS } from '@/features/obligations/types';
import { formatDate } from '@/lib/format-date';
import { HomeSection } from './home-section';
import type { CaseManagerRowDto } from '../types';

const BOARD_LINK = '/educator/admin/compliance';

// Sorted by STUDENT by default (never by staff) — no per-staff ranking
// anywhere on this table, only counts/rows a case manager can act on.
const columns: TableColumn<CaseManagerRowDto>[] = [
  {
    key: 'student',
    header: 'Student',
    cell: (row) => row.studentName,
    sortValue: (row) => row.studentName,
  },
  {
    key: 'caseManager',
    header: 'Case manager',
    cell: (row) => row.caseManagerName ?? 'Unassigned',
  },
  {
    key: 'kind',
    header: 'Type',
    cell: (row) => OBLIGATION_KIND_LABELS[row.kind],
  },
  {
    key: 'dueDate',
    header: 'Due date',
    cell: (row) => formatDate(row.dueDate),
    sortValue: (row) => row.dueDate ?? '',
  },
  {
    key: 'status',
    header: 'Status',
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
  const truncated = total != null && total > rows.length;
  return (
    <HomeSection title="Overdue / at-risk" data-testid="home-overdue-by-case-manager">
      <Table
        label="Overdue and at-risk students"
        data-testid="home-overdue-by-case-manager-table"
        columns={columns}
        rows={rows}
        rowKey={(row) => `${row.studentId}-${row.kind}`}
        rowHref={(row) => `/educator/students/${row.studentId}`}
        defaultSort={{ key: 'student', direction: 'asc' }}
        empty={
          <EmptyState
            icon={CalendarCheck2}
            title="Nothing overdue or at risk"
            description="Every procedural deadline in scope is on track."
          />
        }
      />
      {truncated && (
        <p
          className="mt-3 text-sm text-brand-slate-500"
          data-testid="home-overdue-by-case-manager-more"
        >
          Showing {rows.length} of {total}.{' '}
          <Link to={BOARD_LINK} className="text-brand-teal-600 hover:underline">
            View the compliance board
          </Link>
        </p>
      )}
    </HomeSection>
  );
}
