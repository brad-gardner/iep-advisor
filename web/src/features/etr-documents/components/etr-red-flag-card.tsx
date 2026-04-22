import { AlertTriangle, AlertOctagon, Info, Shield } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { EtrRedFlag } from '../types';

interface EtrRedFlagCardProps {
  redFlag: EtrRedFlag;
}

function severityConfig(severity: string) {
  switch (severity) {
    case 'high':
      return {
        Icon: AlertOctagon,
        iconClass: 'text-brand-red',
        bg: 'bg-red-50',
        border: 'border-red-200',
        titleClass: 'text-brand-red',
        badgeVariant: 'error' as const,
      };
    case 'medium':
      return {
        Icon: AlertTriangle,
        iconClass: 'text-brand-amber-500',
        bg: 'bg-brand-amber-50',
        border: 'border-brand-amber-100',
        titleClass: 'text-brand-amber-500',
        badgeVariant: 'warning' as const,
      };
    default:
      return {
        Icon: Info,
        iconClass: 'text-brand-slate-500',
        bg: 'bg-brand-slate-50',
        border: 'border-brand-slate-200',
        titleClass: 'text-brand-slate-600',
        badgeVariant: 'neutral' as const,
      };
  }
}

export function EtrRedFlagCard({ redFlag }: EtrRedFlagCardProps) {
  const cfg = severityConfig(redFlag.severity);
  const Icon = cfg.Icon;

  return (
    <div
      className={`rounded-card border p-4 ${cfg.bg} ${cfg.border}`}
      data-testid="etr-red-flag-card"
    >
      <div className="flex items-start gap-3">
        <Icon
          className={`w-5 h-5 ${cfg.iconClass} shrink-0 mt-0.5`}
          strokeWidth={1.8}
          aria-hidden="true"
        />
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap mb-1">
            <Badge variant={cfg.badgeVariant}>{redFlag.severity.toUpperCase()}</Badge>
            {redFlag.category && (
              <Badge variant="neutral">{redFlag.category}</Badge>
            )}
          </div>
          <h4 className={`text-[13px] font-semibold ${cfg.titleClass}`}>
            {redFlag.finding}
          </h4>
          <p className="text-sm text-brand-slate-600 mt-1">{redFlag.why_it_matters}</p>
          {redFlag.parent_right_implicated && (
            <div className="mt-2 inline-flex items-center gap-1.5 text-[11px] text-brand-teal-600 bg-brand-teal-50 border border-brand-teal-100 rounded-badge px-2 py-0.5">
              <Shield className="w-3 h-3" strokeWidth={1.8} aria-hidden="true" />
              <span>{redFlag.parent_right_implicated}</span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
