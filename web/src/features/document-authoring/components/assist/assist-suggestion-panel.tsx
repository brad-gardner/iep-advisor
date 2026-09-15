import { Button } from '@/components/ui/button';
import type { AssistCitation } from '../../api/assist-types';

interface AssistSuggestionPanelProps {
  suggestion: string;
  rationale?: string | null;
  citations?: AssistCitation[];
  missingBaseline?: boolean;
  // Omit to render the suggestion read-only (display-only, Dismiss only).
  onAccept?: () => void;
  onDismiss: () => void;
  testIdPrefix: string;
}

// Renders a returned suggestion with its rationale and the evidence it cited
// (source label + excerpt), plus Accept (optional) + Dismiss. A suggestion with
// no citations is labelled so: the case manager knows it is not grounded in
// anything on record.
export function AssistSuggestionPanel({
  suggestion,
  rationale,
  citations = [],
  missingBaseline = false,
  onAccept,
  onDismiss,
  testIdPrefix,
}: AssistSuggestionPanelProps) {
  return (
    <div
      className="mt-2 rounded-card border border-brand-teal-100 bg-brand-teal-50 p-3"
      data-testid={`${testIdPrefix}-suggestion`}
    >
      {missingBaseline && (
        <p
          className="mb-2 rounded-input border border-brand-amber-200 bg-brand-amber-50 px-2 py-1 text-[12px] text-brand-amber-700"
          data-testid={`${testIdPrefix}-missing-baseline`}
        >
          No baseline on record for this goal — add a current measurement or ask the provider before relying on a number.
        </p>
      )}
      <p className="whitespace-pre-wrap text-[13px] leading-relaxed text-brand-slate-700">{suggestion}</p>
      {rationale && (
        <p className="mt-2 text-[12px] italic text-brand-slate-500" data-testid={`${testIdPrefix}-rationale`}>
          {rationale}
        </p>
      )}
      <div className="mt-2 text-[12px] text-brand-slate-500" data-testid={`${testIdPrefix}-citations`}>
        {citations.length === 0 ? (
          <span>Not grounded in evidence on record.</span>
        ) : (
          <ul className="space-y-1">
            {citations.map((c) => (
              <li key={c.evidenceId} className="flex gap-1.5">
                <span className="shrink-0 rounded bg-white px-1 font-mono text-[11px] text-brand-teal-700">{c.evidenceId}</span>
                <span>
                  <span className="font-medium text-brand-slate-600">{c.sourceLabel}</span>
                  {c.excerpt ? ` — ${c.excerpt}` : ''}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
      <div className="mt-3 flex items-center justify-end gap-2">
        {onAccept && (
          <Button variant="primary" className="px-3 py-1.5" onClick={onAccept} data-testid={`${testIdPrefix}-accept`}>
            Accept
          </Button>
        )}
        <Button variant="ghost" className="px-3 py-1.5" onClick={onDismiss} data-testid={`${testIdPrefix}-dismiss`}>
          Dismiss
        </Button>
      </div>
    </div>
  );
}
