import { Badge } from '@/components/ui/badge';
import { goalStatusLabel } from '../lib/status-label';
import type { GoalRecordStatus } from '../types';

const variantByStatus: Record<GoalRecordStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  Active: 'success',
  Met: 'success',
  NotMet: 'error',
  Retired: 'neutral',
  Carried: 'neutral',
};

export function GoalStatusBadge({ status }: { status: GoalRecordStatus }) {
  return (
    <Badge variant={variantByStatus[status]} data-testid={`goal-status-${status}`}>
      {goalStatusLabel(status)}
    </Badge>
  );
}
