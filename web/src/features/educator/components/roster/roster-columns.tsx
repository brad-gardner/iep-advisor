import { Badge } from '@/components/ui/badge';
import type { TableColumn } from '@/components/ui/table';
import { GRADE_LEVEL_LABELS } from '../../types';
import type { SchoolStudent } from '../../types';
import { StudentStatusBadge } from '../student-status-badge';

export function studentDisplayName(student: SchoolStudent): string {
  return `${student.firstName} ${student.lastName ?? ''}`.trim();
}

// Roster columns. Sorting is page-local (the server pages; the Table sorts the
// rows it was given), which is why `sortValue` stays on the simple columns.
export function rosterColumns(options: { showSchool: boolean }): TableColumn<SchoolStudent>[] {
  const columns: TableColumn<SchoolStudent>[] = [
    {
      key: 'name',
      header: 'Student',
      cell: studentDisplayName,
      sortValue: (s) => studentDisplayName(s).toLowerCase(),
    },
    {
      key: 'externalId',
      header: 'Student ID',
      hideBelow: 'md',
      cell: (s) => s.externalStudentId || '—',
      sortValue: (s) => s.externalStudentId ?? '',
    },
  ];

  if (options.showSchool) {
    columns.push({
      key: 'school',
      header: 'School',
      hideBelow: 'md',
      cell: (s) => (s.schoolName ? <Badge variant="neutral">{s.schoolName}</Badge> : '—'),
      sortValue: (s) => s.schoolName ?? '',
    });
  }

  columns.push(
    {
      key: 'grade',
      header: 'Grade',
      align: 'right',
      hideBelow: 'md',
      cell: (s) => (s.gradeLevel ? GRADE_LEVEL_LABELS[s.gradeLevel] : '—'),
      sortValue: (s) => s.gradeLevel ?? '',
    },
    {
      key: 'status',
      header: 'Status',
      cell: (s) => <StudentStatusBadge status={s.status} />,
      sortValue: (s) => s.status,
    },
    {
      key: 'caseManager',
      header: 'Case manager',
      hideBelow: 'lg',
      cell: (s) => s.caseManagerName || '—',
      sortValue: (s) => s.caseManagerName ?? '',
    }
  );

  return columns;
}
