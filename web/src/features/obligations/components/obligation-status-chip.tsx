import { AlertTriangle, CalendarCheck2, CalendarClock, HelpCircle, type LucideIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { OBLIGATION_STATUS_LABELS } from '../types';
import type { ObligationStatus } from '../types';

const config: Record<ObligationStatus, { variant: 'success' | 'warning' | 'error' | 'neutral'; Icon: LucideIcon }> = {
  Upcoming: { variant: 'success', Icon: CalendarCheck2 },
  DueSoon: { variant: 'warning', Icon: CalendarClock },
  Overdue: { variant: 'error', Icon: AlertTriangle },
  Unknown: { variant: 'neutral', Icon: HelpCircle },
};

/** Status chip for a procedural deadline: icon + text, never colour alone. */
export function ObligationStatusChip({ status }: { status: ObligationStatus }) {
  const { variant, Icon } = config[status];
  return (
    <Badge variant={variant} className="inline-flex items-center gap-1" data-testid={`obligation-status-${status}`}>
      <Icon className="h-3 w-3" strokeWidth={2} aria-hidden="true" />
      {OBLIGATION_STATUS_LABELS[status]}
    </Badge>
  );
}
