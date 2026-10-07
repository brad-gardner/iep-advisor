import { useTranslation } from 'react-i18next';
import { AlertTriangle, CalendarCheck2, CalendarClock, HelpCircle, type LucideIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { ObligationStatus } from '../types';

const config: Record<ObligationStatus, { variant: 'success' | 'warning' | 'error' | 'neutral'; Icon: LucideIcon }> = {
  Upcoming: { variant: 'success', Icon: CalendarCheck2 },
  DueSoon: { variant: 'warning', Icon: CalendarClock },
  Overdue: { variant: 'error', Icon: AlertTriangle },
  Unknown: { variant: 'neutral', Icon: HelpCircle },
};

/** Status chip for a procedural deadline: icon + text, never colour alone.
 * `useTranslation('obligations')` here (rather than relying only on
 * `@/lib/obligation-label`'s plain-function `obligationStatusLabel`, which
 * this chip rendered via before) is what makes a language switch re-render
 * this chip once that staff-only namespace's Spanish finishes loading —
 * every caller renders this from across several features, so the
 * subscription has to live here rather than in each of them. */
export function ObligationStatusChip({ status }: { status: ObligationStatus }) {
  const { t } = useTranslation('obligations');
  const { variant, Icon } = config[status];
  return (
    <Badge variant={variant} className="inline-flex items-center gap-1" data-testid={`obligation-status-${status}`}>
      <Icon className="h-3 w-3" strokeWidth={2} aria-hidden="true" />
      {t(`status.${status}`, { defaultValue: status })}
    </Badge>
  );
}
