import { Check, Minus, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { BriefChecklistItemDto } from '../types';

/** Procedural checklist: required participants present, notice sent in time,
 *  family input received (plan 7, decision 2) — ✓ satisfied, ✗ not, — unknown.
 *  `item.label`/`item.detail` are server-composed, deterministic strings
 *  (not AI-derived, and not yet localized server-side) — only this
 *  component's own chrome (the empty state, the icon labels) is translated
 *  here. */
export function BriefChecklist({ items }: { items: BriefChecklistItemDto[] }) {
  const { t } = useTranslation('meeting-brief');
  if (items.length === 0) {
    return (
      <p className="text-sm text-brand-slate-500" data-testid="brief-checklist-empty">
        {t('checklist.empty')}
      </p>
    );
  }

  return (
    <ul className="space-y-2" data-testid="brief-checklist">
      {items.map((item) => (
        <li key={item.key} className="flex items-start gap-2" data-testid={`brief-checklist-item-${item.key}`}>
          <ChecklistIcon satisfied={item.satisfied} />
          <div>
            <p className="text-sm text-brand-slate-800">{item.label}</p>
            {item.detail && <p className="text-xs text-brand-slate-500">{item.detail}</p>}
          </div>
        </li>
      ))}
    </ul>
  );
}

function ChecklistIcon({ satisfied }: { satisfied: boolean | null }) {
  const { t } = useTranslation('meeting-brief');
  if (satisfied === true) {
    return (
      <span
        className="mt-0.5 text-brand-teal-600"
        aria-label={t('checklist.satisfied')}
        data-testid="checklist-icon-satisfied"
      >
        <Check className="h-4 w-4" aria-hidden="true" />
      </span>
    );
  }
  if (satisfied === false) {
    return (
      <span
        className="mt-0.5 text-brand-danger-700"
        aria-label={t('checklist.notSatisfied')}
        data-testid="checklist-icon-unsatisfied"
      >
        <X className="h-4 w-4" aria-hidden="true" />
      </span>
    );
  }
  return (
    <span className="mt-0.5 text-brand-slate-500" aria-label={t('checklist.unknown')} data-testid="checklist-icon-unknown">
      <Minus className="h-4 w-4" aria-hidden="true" />
    </span>
  );
}
