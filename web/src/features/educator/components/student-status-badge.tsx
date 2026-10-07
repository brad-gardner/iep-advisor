import { Badge } from '@/components/ui/badge';
import { studentStatusLabel } from '../lib/student-enum-labels';
import type { StudentStatus } from '../types';

const VARIANT: Record<StudentStatus, 'success' | 'warning' | 'neutral'> = {
  Active: 'success',
  Exited: 'warning',
  Archived: 'neutral',
};

// Lifecycle status chip. Always carries the label text, so the state is never
// conveyed by colour alone.
export function StudentStatusBadge({ status }: { status: StudentStatus }) {
  return (
    <Badge variant={VARIANT[status]} data-testid={`student-status-${status}`}>
      {studentStatusLabel(status)}
    </Badge>
  );
}
