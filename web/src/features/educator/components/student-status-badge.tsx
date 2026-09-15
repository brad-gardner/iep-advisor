import { Badge } from '@/components/ui/badge';
import { STUDENT_STATUS_LABELS } from '../types';
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
      {STUDENT_STATUS_LABELS[status]}
    </Badge>
  );
}
