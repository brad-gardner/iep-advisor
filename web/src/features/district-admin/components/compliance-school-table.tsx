import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Table, type TableColumn } from '@/components/ui/table';
import { EmptyState } from '@/components/ui/empty-state';
import { School } from 'lucide-react';
import { StackedBarChart, type StackedBarRow } from '@/components/ui/charts/stacked-bar';
import { districtDrillHref } from '../lib/drill-link';
import type { ComplianceSchoolRowDto } from '../types';

type DrillColumnKey =
  | 'overdueAnnual'
  | 'overdueReeval'
  | 'due30'
  | 'due60'
  | 'unknownDates'
  | 'noLead'
  | 'dueInRange';

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

function toStackedRows(rows: ComplianceSchoolRowDto[], t: TFunction<'district-admin'>): StackedBarRow[] {
  return rows.map((row) => ({
    label: row.schoolName,
    segments: [
      { key: 'overdueAnnual', label: t('complianceSchoolTable.chartSegments.overdueAnnual'), value: row.overdueAnnual },
      { key: 'overdueReeval', label: t('complianceSchoolTable.chartSegments.overdueReeval'), value: row.overdueReeval },
      { key: 'due30', label: t('complianceSchoolTable.chartSegments.due30'), value: row.due30 },
      { key: 'due60', label: t('complianceSchoolTable.chartSegments.due60'), value: row.due60 },
      { key: 'unknownDates', label: t('complianceSchoolTable.chartSegments.unknownDates'), value: row.unknownDates },
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
  const { t } = useTranslation('district-admin');
  const columns: TableColumn<ComplianceSchoolRowDto>[] = [
    {
      key: 'school',
      header: t('complianceSchoolTable.columns.school'),
      cell: (row) => row.schoolName,
      sortValue: (row) => row.schoolName,
    },
    {
      key: 'activeStudents',
      header: t('complianceSchoolTable.columns.activeStudents'),
      align: 'right',
      cell: (row) => row.activeStudents,
      sortValue: (row) => row.activeStudents,
    },
    drillColumn('overdueAnnual', t('complianceSchoolTable.columns.overdueAnnual'), drill),
    drillColumn('overdueReeval', t('complianceSchoolTable.columns.overdueReeval'), drill),
    drillColumn('due30', t('complianceSchoolTable.columns.due30'), drill),
    drillColumn('due60', t('complianceSchoolTable.columns.due60'), drill),
    drillColumn('dueInRange', t('complianceSchoolTable.columns.dueInRange'), drill),
    drillColumn('unknownDates', t('complianceSchoolTable.columns.unknownDates'), drill),
    drillColumn('noLead', t('complianceSchoolTable.columns.noLead'), drill),
  ];

  return (
    <div className="space-y-4" data-testid="compliance-school-section">
      {rows.length > 1 && (
        <StackedBarChart
          title={t('complianceSchoolTable.chartTitle')}
          rows={toStackedRows(rows, t)}
          data-testid="compliance-school-chart"
        />
      )}
      <Table
        label={t('complianceSchoolTable.tableLabel')}
        data-testid="compliance-school-table"
        columns={columns}
        rows={rows}
        rowKey={(row) => row.schoolId}
        defaultSort={{ key: 'school', direction: 'asc' }}
        empty={
          <EmptyState
            icon={School}
            title={t('complianceSchoolTable.emptyTitle')}
            description={t('complianceSchoolTable.emptyDescription')}
          />
        }
      />
    </div>
  );
}
