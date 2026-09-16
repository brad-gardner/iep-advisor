import { Table, type TableColumn } from '@/components/ui/table';
import { EmptyState } from '@/components/ui/empty-state';
import { CalendarCheck2 } from 'lucide-react';
import { ObligationStatusChip } from '@/features/obligations/components/obligation-status-chip';
import { OBLIGATION_KIND_LABELS } from '@/features/obligations/types';
import { formatDate } from '@/lib/format-date';
import { HomeSection } from './home-section';
import type { CaseManagerRowDto } from '../types';

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

/** SchoolAdmin/DistrictAdmin "Overdue / at-risk" table — deliberately sorted
 * by student, never by case manager, so it reads as work to do rather than a
 * staff scorecard. */
export function OverdueByCaseManagerTable({ rows }: { rows: CaseManagerRowDto[] }) {
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
    </HomeSection>
  );
}
