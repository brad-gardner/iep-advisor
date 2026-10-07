import { AlertTriangle, CheckCircle2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import type { EtrCompletenessPayload } from '@/features/analysis/types';

interface EtrAssessmentCompletenessViewProps {
  data: EtrCompletenessPayload;
}

export function EtrAssessmentCompletenessView({ data }: EtrAssessmentCompletenessViewProps) {
  const { t } = useTranslation('etr-documents');

  function adequacyBadge(rating: string) {
    switch (rating) {
      case 'strong':
        return { variant: 'success' as const, label: t('completenessView.ratingStrong') };
      case 'adequate':
        return { variant: 'info' as const, label: t('completenessView.ratingAdequate') };
      case 'thin':
        return { variant: 'warning' as const, label: t('completenessView.ratingThin') };
      case 'missing':
      case 'concerning':
        return {
          variant: 'error' as const,
          label: rating === 'missing' ? t('completenessView.ratingMissing') : t('completenessView.ratingConcerning'),
        };
      default:
        return { variant: 'neutral' as const, label: rating || t('completenessView.ratingUnknown') };
    }
  }

  const overall = adequacyBadge(data.overallCompletenessRating);

  return (
    <div className="space-y-6" data-testid="etr-assessment-completeness">
      <section>
        <div className="flex items-center justify-between mb-3">
          <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800">
            {t('completenessView.heading')}
          </h2>
          <div className="flex items-center gap-2">
            <span className="text-[11px] text-brand-slate-500 uppercase tracking-wide">
              {t('completenessView.overall')}
            </span>
            <Badge variant={overall.variant}>{overall.label}</Badge>
          </div>
        </div>
      </section>

      <section>
        <h3 className="text-sm font-semibold text-brand-slate-800 mb-2">
          {t('completenessView.evaluatedDomains', { count: data.evaluatedDomains.length })}
        </h3>
        {data.evaluatedDomains.length === 0 ? (
          <p className="text-sm text-brand-slate-500">{t('completenessView.noEvaluatedDomains')}</p>
        ) : (
          <div className="space-y-2">
            {data.evaluatedDomains.map((d, i) => {
              const b = adequacyBadge(d.adequacyRating);
              const tools = d.toolsUsed.join(', ');
              return (
                <div
                  key={i}
                  className="rounded-card border border-brand-slate-200 bg-white p-3"
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
                            {t('completenessView.tools', { tools })}
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
          {t('completenessView.missingDomains', { count: data.missingDomains.length })}
        </h3>
        {data.missingDomains.length === 0 ? (
          <p className="text-sm text-brand-slate-500">
            {t('completenessView.noMissingDomains')}
          </p>
        ) : (
          <div className="space-y-2">
            {data.missingDomains.map((m, i) => (
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
