import { Badge } from '@/components/ui/badge';
import { GOAL_RECORD_STATUS_LABELS } from '../types';
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
      {GOAL_RECORD_STATUS_LABELS[status]}
    </Badge>
  );
}
