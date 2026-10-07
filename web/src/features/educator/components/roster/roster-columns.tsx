import { Badge } from '@/components/ui/badge';
import type { TableColumn } from '@/components/ui/table';
import i18n from '@/lib/i18n';
import { gradeLevelLabel } from '@/lib/grade-level-label';
import type { SchoolStudent } from '../../types';
import { StudentStatusBadge } from '../student-status-badge';

export function studentDisplayName(student: SchoolStudent): string {
  return `${student.firstName} ${student.lastName ?? ''}`.trim();
}

// Roster columns. Sorting is page-local (the server pages; the Table sorts the
// rows it was given), which is why `sortValue` stays on the simple columns.
// Header text uses the plain `i18n.t` singleton (not a hook — this is a
// builder function, not a component) and is called fresh on every render of
// the host page rather than memoized, so a language switch is reflected
// immediately (see `educator-students-page.tsx`).
export function rosterColumns(options: { showSchool: boolean }): TableColumn<SchoolStudent>[] {
  const t = i18n.t;
  const columns: TableColumn<SchoolStudent>[] = [
    {
      key: 'name',
      header: t('educator:rosterColumns.student'),
      cell: studentDisplayName,
      sortValue: (s) => studentDisplayName(s).toLowerCase(),
    },
    {
      key: 'externalId',
      header: t('educator:rosterColumns.studentId'),
      hideBelow: 'md',
      cell: (s) => s.externalStudentId || '—',
      sortValue: (s) => s.externalStudentId ?? '',
    },
  ];

  if (options.showSchool) {
    columns.push({
      key: 'school',
      header: t('educator:rosterColumns.school'),
      hideBelow: 'md',
      cell: (s) => (s.schoolName ? <Badge variant="neutral">{s.schoolName}</Badge> : '—'),
      sortValue: (s) => s.schoolName ?? '',
    });
  }

  columns.push(
    {
      key: 'grade',
      header: t('educator:rosterColumns.grade'),
      align: 'right',
      hideBelow: 'md',
      cell: (s) => (s.gradeLevel ? gradeLevelLabel(s.gradeLevel) : '—'),
      sortValue: (s) => s.gradeLevel ?? '',
    },
    {
      key: 'status',
      header: t('educator:rosterColumns.status'),
      cell: (s) => <StudentStatusBadge status={s.status} />,
      sortValue: (s) => s.status,
    },
    {
      key: 'caseManager',
      header: t('educator:rosterColumns.caseManager'),
      hideBelow: 'lg',
      cell: (s) => s.caseManagerName || '—',
      sortValue: (s) => s.caseManagerName ?? '',
    }
  );

  return columns;
}
