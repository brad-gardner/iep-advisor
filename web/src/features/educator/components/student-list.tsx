import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import type { SchoolStudent } from '../types';

interface StudentListProps {
  students: SchoolStudent[];
}

export function StudentList({ students }: StudentListProps) {
  if (students.length === 0) {
    return (
      <p className="text-brand-slate-400 text-sm" data-testid="student-list-empty">
        No students yet. Add one to get started.
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
            <Card className="hover:border-brand-teal-400 transition-colors flex justify-between items-center">
              <span className="text-brand-slate-800 font-medium">
                {student.firstName} {student.lastName ?? ''}
              </span>
              {student.gradeLevel && (
                <span className="text-sm text-brand-slate-500">{student.gradeLevel}</span>
              )}
            </Card>
          </Link>
        </li>
      ))}
    </ul>
  );
}
