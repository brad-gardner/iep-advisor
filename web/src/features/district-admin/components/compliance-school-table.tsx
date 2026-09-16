import { Link } from 'react-router-dom';
import { Table, type TableColumn } from '@/components/ui/table';
import { EmptyState } from '@/components/ui/empty-state';
import { School } from 'lucide-react';
import { StackedBarChart, type StackedBarRow } from '@/components/ui/charts/stacked-bar';
import { districtDrillHref } from '../lib/drill-link';
import type { ComplianceSchoolRowDto } from '../types';

type DrillColumnKey = 'overdueAnnual' | 'overdueReeval' | 'due30' | 'due60' | 'unknownDates' | 'noLead';

function drillColumn(key: DrillColumnKey, header: string, drill: Record<string, string>): TableColumn<ComplianceSchoolRowDto> {
  return {
    key,
    header,
    align: 'right',
    sortValue: (row) => row[key],
    cell: (row) => (
      <Link
        to={districtDrillHref(drill, key, row.schoolId)}
        className="text-brand-teal-600 hover:underline"
        data-testid={`compliance-school-${row.schoolId}-${key}`}
      >
        {row[key]}
      </Link>
    ),
  };
}

function toStackedRows(rows: ComplianceSchoolRowDto[]): StackedBarRow[] {
  return rows.map((row) => ({
    label: row.schoolName,
    segments: [
      { key: 'overdueAnnual', label: 'Overdue annual', value: row.overdueAnnual },
      { key: 'overdueReeval', label: 'Overdue reeval', value: row.overdueReeval },
      { key: 'due30', label: 'Due 30 days', value: row.due30 },
      { key: 'due60', label: 'Due 60 days', value: row.due60 },
      { key: 'unknownDates', label: 'Unknown dates', value: row.unknownDates },
    ],
  }));
}

/** Per-school compliance rows — the same numbers as the summary tiles, broken
 * out by school, each numeric cell drilling to the matching roster filter. */
export function ComplianceSchoolTable({
  rows,
  drill,
}: {
  rows: ComplianceSchoolRowDto[];
  drill: Record<string, string>;
}) {
  const columns: TableColumn<ComplianceSchoolRowDto>[] = [
    {
      key: 'school',
      header: 'School',
      cell: (row) => row.schoolName,
      sortValue: (row) => row.schoolName,
    },
    {
      key: 'activeStudents',
      header: 'Active students',
      align: 'right',
      cell: (row) => row.activeStudents,
      sortValue: (row) => row.activeStudents,
    },
    drillColumn('overdueAnnual', 'Overdue annual', drill),
    drillColumn('overdueReeval', 'Overdue reeval', drill),
    drillColumn('due30', 'Due 30d', drill),
    drillColumn('due60', 'Due 60d', drill),
    drillColumn('unknownDates', 'Unknown dates', drill),
    drillColumn('noLead', 'No case manager', drill),
  ];

  return (
    <div className="space-y-4" data-testid="compliance-school-section">
      {rows.length > 1 && (
        <StackedBarChart
          title="Deadline buckets by school"
          rows={toStackedRows(rows)}
          data-testid="compliance-school-chart"
        />
      )}
      <Table
        label="Compliance by school"
        data-testid="compliance-school-table"
        columns={columns}
        rows={rows}
        rowKey={(row) => row.schoolId}
        defaultSort={{ key: 'school', direction: 'asc' }}
        empty={
          <EmptyState icon={School} title="No schools in scope" description="Add a school to see compliance data." />
        }
      />
    </div>
  );
}
