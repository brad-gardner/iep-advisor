import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import type { SchoolStudent } from '../types';

interface StudentListProps {
  students: SchoolStudent[];
  // Role-specific empty state copy (e.g. the teacher "no students assigned"
  // message); defaults to the generic prompt.
  emptyMessage?: string;
  // Show each student's school as a badge (DistrictAdmin district-wide roster).
  showSchool?: boolean;
}

export function StudentList({
  students,
  emptyMessage = 'No students yet. Add one to get started.',
  showSchool = false,
}: StudentListProps) {
  if (students.length === 0) {
    return (
      <p className="text-brand-slate-400 text-sm" data-testid="student-list-empty">
        {emptyMessage}
      </p>
    );
  }

  return (
    <ul className="space-y-2" data-testid="student-list">
      {students.map((student) => (
        <li key={student.id}>
          <Link
            to={`/educator/students/${student.id}`}
            data-testid={`student-row-${student.id}`}
            className="block"
          >
            <Card className="hover:border-brand-teal-400 transition-colors flex justify-between items-center gap-3">
              <span className="text-brand-slate-800 font-medium">
                {student.firstName} {student.lastName ?? ''}
              </span>
              <span className="flex items-center gap-2">
                {showSchool && student.schoolName && (
                  <Badge variant="neutral">{student.schoolName}</Badge>
                )}
                {student.gradeLevel && (
                  <span className="text-sm text-brand-slate-500">{student.gradeLevel}</span>
                )}
              </span>
            </Card>
          </Link>
        </li>
      ))}
    </ul>
  );
}
