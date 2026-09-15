import { AlertCircle, CheckCircle2, Lightbulb } from 'lucide-react';
import { Card } from '@/components/ui/card';
import type { CompletenessSummary } from '../lib/completeness';
import { sectionDomId } from '../lib/section-dom';

interface CompletenessPanelProps {
  summary: CompletenessSummary;
}

/**
 * Advisory completeness sidebar. Required gaps are what finalize will reject;
 * advisory items are coaching (missing baseline, service without frequency)
 * and never block — Steph can always finalize over a warning.
 */
export function CompletenessPanel({ summary }: CompletenessPanelProps) {
  const required = summary.items.filter((i) => i.severity === 'required');
  const advisory = summary.items.filter((i) => i.severity === 'advisory');

  const jump = (sectionId: number, fieldKey: string) => {
    const field = document.querySelector<HTMLElement>(`[data-testid="field-${fieldKey}"]`);
    const target = field ?? document.getElementById(sectionDomId(sectionId));
    target?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    if (field && 'focus' in field) {
      const focusable = field.matches('input,textarea,select') ? field : field.querySelector<HTMLElement>('input,textarea,select');
      focusable?.focus({ preventScroll: true });
    }
  };

  return (
    <aside className="lg:sticky lg:top-4 lg:self-start" aria-label="Completeness" data-testid="completeness-panel">
      <Card>
        <div className="mb-3 flex items-center justify-between">
          <h2 className="font-serif text-base text-brand-slate-800">Completeness</h2>
          <span className="text-sm font-medium text-brand-slate-600" data-testid="completeness-percent">
            {summary.percent}%
          </span>
        </div>
        <div className="mb-4 h-1.5 overflow-hidden rounded-full bg-brand-slate-100" aria-hidden="true">
          <div className="h-full rounded-full bg-brand-teal-500 transition-all" style={{ width: `${summary.percent}%` }} />
        </div>

        {summary.items.length === 0 ? (
          <p className="flex items-center gap-2 text-sm text-brand-slate-600">
            <CheckCircle2 className="h-4 w-4 text-brand-teal-500" aria-hidden="true" />
            Nothing flagged
          </p>
        ) : (
          <ul className="space-y-2 text-sm">
            {required.map((item) => (
              <li key={item.key}>
                <button
                  type="button"
                  onClick={() => jump(item.sectionId, item.fieldKey)}
                  className="flex w-full items-start gap-2 text-left text-brand-slate-700 hover:underline"
                >
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-brand-danger-700" aria-hidden="true" />
                  <span>{item.message}</span>
                </button>
              </li>
            ))}
            {advisory.map((item) => (
              <li key={item.key}>
                <button
                  type="button"
                  onClick={() => jump(item.sectionId, item.fieldKey)}
                  className="flex w-full items-start gap-2 text-left text-brand-slate-600 hover:underline"
                >
                  <Lightbulb className="mt-0.5 h-4 w-4 shrink-0 text-amber-500" aria-hidden="true" />
                  <span>{item.message}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
        {advisory.length > 0 && (
          <p className="mt-3 text-xs text-brand-slate-400">Suggestions never block finalizing.</p>
        )}
      </Card>
    </aside>
  );
}
