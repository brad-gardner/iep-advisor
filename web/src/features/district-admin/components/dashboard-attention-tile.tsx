import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import type { DashboardNoParentStudent, DashboardStudent } from '../types';

const MAX_INLINE_STUDENTS = 5;

interface DashboardAttentionTileProps {
  studentsWithoutStaff: DashboardStudent[];
  studentsWithoutParent: DashboardNoParentStudent[];
  // False when the district has no active students yet — switches both
  // sections to a single setup-oriented empty state instead of celebrating.
  hasStudents: boolean;
}

function studentName(student: DashboardStudent): string {
  return [student.firstName, student.lastName ?? ''].join(' ').trim();
}

interface AttentionSectionProps<T extends DashboardStudent> {
  title: string;
  testId: string;
  students: T[];
  emptyMessage: string;
  viewAllTo: string;
  // Extra badge rendered per row (the no-parent invite-status distinction).
  renderStatus?: (student: T) => React.ReactNode;
}

function AttentionSection<T extends DashboardStudent>({
  title,
  testId,
  students,
  emptyMessage,
  viewAllTo,
  renderStatus,
}: AttentionSectionProps<T>) {
  return (
    <section data-testid={testId}>
      <h3 className="text-sm font-medium text-brand-slate-800 mb-2">{title}</h3>
      {students.length === 0 ? (
        <p className="text-sm text-brand-teal-600" data-testid={`${testId}-empty`}>
          {emptyMessage}
        </p>
      ) : (
        <>
          <ul className="space-y-2 text-sm">
            {students.slice(0, MAX_INLINE_STUDENTS).map((student) => (
              <li
                key={student.schoolStudentId}
                className="flex items-center justify-between gap-3"
                data-testid={`${testId}-${student.schoolStudentId}`}
              >
                <span className="text-brand-slate-800">{studentName(student)}</span>
                <span className="flex items-center gap-2">
                  {renderStatus?.(student)}
                  <span className="text-xs text-brand-slate-400">
                    {student.schoolName}
                  </span>
                </span>
              </li>
            ))}
          </ul>
          <div className="mt-2">
            <Link
              to={viewAllTo}
              className="text-sm text-brand-teal-600 hover:underline"
              data-testid={`${testId}-view-all`}
            >
              View all ({students.length})
            </Link>
          </div>
        </>
      )}
    </section>
  );
}

// "Needs attention" lists: students with no assigned staff and students with no
// linked parent. Presentational: the composing container owns the single
// dashboard fetch.
export function DashboardAttentionTile({
  studentsWithoutStaff,
  studentsWithoutParent,
  hasStudents,
}: DashboardAttentionTileProps) {
  return (
    <Card data-testid="dashboard-attention-tile">
      <h2 className="font-serif text-xl mb-4">Needs attention</h2>

      {!hasStudents ? (
        <p
          className="text-sm text-brand-slate-400"
          data-testid="dashboard-attention-tile-empty"
        >
          Once students are added, anyone missing assigned staff or a linked
          parent will show up here.
        </p>
      ) : (
        <div className="space-y-5">
          <AttentionSection
            title="No assigned staff"
            testId="dashboard-attention-no-staff"
            students={studentsWithoutStaff}
            emptyMessage="All students have assigned staff."
            viewAllTo="/educator/students?attention=no-staff"
          />
          <AttentionSection
            title="No linked parent"
            testId="dashboard-attention-no-parent"
            students={studentsWithoutParent}
            emptyMessage="All students have a linked parent."
            viewAllTo="/educator/students?attention=no-parent"
            renderStatus={(student) =>
              student.parentInvitePending ? (
                <Badge variant="warning">Invite pending</Badge>
              ) : (
                <Badge variant="neutral">Not invited</Badge>
              )
            }
          />
        </div>
      )}
    </Card>
  );
}
