import { Badge } from '@/components/ui/badge';
import { RESOURCE_COMMITMENT_KIND_LABELS } from '../types';
import type { ResourceCommitmentDto } from '../types';

interface ResourceCommitmentListProps {
  items: ResourceCommitmentDto[];
}

/** New/changed services, placement, ESY, 1:1, and transportation calls out in
 *  the brief (plan 7, decision 2) — deterministic, not AI-derived. */
export function ResourceCommitmentList({ items }: ResourceCommitmentListProps) {
  if (items.length === 0) {
    return (
      <p className="text-sm text-brand-slate-400" data-testid="brief-resource-commitments-empty">
        No resource commitments detected in this draft.
      </p>
    );
  }

  return (
    <ul className="space-y-2" data-testid="brief-resource-commitments">
      {items.map((item, i) => (
        <li key={`${item.fieldKey}-${item.rowId ?? i}`} className="rounded-card border border-brand-slate-100 p-3">
          <div className="mb-1 flex items-center gap-2">
            <Badge variant="info">{RESOURCE_COMMITMENT_KIND_LABELS[item.kind]}</Badge>
            <span className="text-sm font-medium text-brand-slate-800">{item.label}</span>
          </div>
          <p className="text-sm text-brand-slate-600">{item.detail}</p>
        </li>
      ))}
    </ul>
  );
}
