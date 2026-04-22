import { AlertTriangle, CheckCircle2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { AdequacyRating, AssessmentCompleteness } from '../types';

interface EtrAssessmentCompletenessViewProps {
  data: AssessmentCompleteness;
}

function adequacyBadge(rating: AdequacyRating) {
  switch (rating) {
    case 'strong':
      return { variant: 'success' as const, label: 'Strong' };
    case 'adequate':
      return { variant: 'info' as const, label: 'Adequate' };
    case 'thin':
      return { variant: 'warning' as const, label: 'Thin' };
    case 'concerning':
      return { variant: 'error' as const, label: 'Concerning' };
    default:
      return { variant: 'neutral' as const, label: String(rating || 'Unknown') };
  }
}

export function EtrAssessmentCompletenessView({ data }: EtrAssessmentCompletenessViewProps) {
  const overall = adequacyBadge(data.overall_completeness_rating);

  return (
    <div className="space-y-6" data-testid="etr-assessment-completeness">
      <section>
        <div className="flex items-center justify-between mb-3">
          <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800">
            Assessment Completeness
          </h2>
          <div className="flex items-center gap-2">
            <span className="text-[11px] text-brand-slate-400 uppercase tracking-wide">
              Overall
            </span>
            <Badge variant={overall.variant}>{overall.label}</Badge>
          </div>
        </div>
      </section>

      <section>
        <h3 className="text-sm font-semibold text-brand-slate-800 mb-2">
          Evaluated Domains ({data.evaluated_domains.length})
        </h3>
        {data.evaluated_domains.length === 0 ? (
          <p className="text-sm text-brand-slate-400">No evaluated domains reported.</p>
        ) : (
          <div className="space-y-2">
            {data.evaluated_domains.map((d, i) => {
              const b = adequacyBadge(d.adequacy_rating);
              const tools = Array.isArray(d.tools_used)
                ? d.tools_used.join(', ')
                : d.tools_used;
              return (
                <div
                  key={i}
                  className="rounded-card border-[0.5px] border-brand-slate-200 bg-white p-3"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-start gap-2">
                      <CheckCircle2
                        className="w-4 h-4 text-brand-teal-500 shrink-0 mt-0.5"
                        strokeWidth={1.8}
                        aria-hidden="true"
                      />
                      <div>
                        <p className="text-sm font-medium text-brand-slate-800">
                          {d.domain}
                        </p>
                        {tools && (
                          <p className="text-[12px] text-brand-slate-500 mt-0.5">
                            Tools: {tools}
                          </p>
                        )}
                        {d.notes && (
                          <p className="text-[12px] text-brand-slate-500 mt-1">
                            {d.notes}
                          </p>
                        )}
                      </div>
                    </div>
                    <Badge variant={b.variant}>{b.label}</Badge>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </section>

      <section>
        <h3 className="text-sm font-semibold text-brand-slate-800 mb-2">
          Missing or Under-Evaluated Domains ({data.missing_domains.length})
        </h3>
        {data.missing_domains.length === 0 ? (
          <p className="text-sm text-brand-slate-400">
            No missing domains identified.
          </p>
        ) : (
          <div className="space-y-2">
            {data.missing_domains.map((m, i) => (
              <div
                key={i}
                className="rounded-card border border-brand-amber-100 bg-brand-amber-50 p-3"
              >
                <div className="flex items-start gap-2">
                  <AlertTriangle
                    className="w-4 h-4 text-brand-amber-500 shrink-0 mt-0.5"
                    strokeWidth={1.8}
                    aria-hidden="true"
                  />
                  <div>
                    <p className="text-sm font-medium text-brand-amber-500">
                      {m.domain}
                    </p>
                    <p className="text-[12px] text-brand-slate-600 mt-0.5">
                      {m.rationale}
                    </p>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
