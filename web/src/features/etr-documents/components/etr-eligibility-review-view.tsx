import { CheckCircle2, XCircle } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import type { EtrEligibilityPayload } from '@/features/analysis/types';

interface EtrEligibilityReviewViewProps {
  data: EtrEligibilityPayload;
}

function EvidenceList({
  title,
  items,
  tone,
}: {
  title: string;
  items: string[];
  tone: 'support' | 'contra' | 'neutral';
}) {
  if (items.length === 0) return null;
  const colorClass =
    tone === 'support'
      ? 'text-brand-teal-600'
      : tone === 'contra'
        ? 'text-brand-danger-700'
        : 'text-brand-slate-600';
  return (
    <div>
      <h4
        className={`text-[11px] uppercase tracking-wide font-semibold mb-2 ${colorClass}`}
      >
        {title}
      </h4>
      <ul className="space-y-1.5 list-disc list-outside pl-5">
        {items.map((item, i) => (
          <li key={i} className="text-sm text-brand-slate-600">
            {item}
          </li>
        ))}
      </ul>
    </div>
  );
}

export function EtrEligibilityReviewView({ data }: EtrEligibilityReviewViewProps) {
  const { t } = useTranslation('etr-documents');
  const supported = data.dataSupportsConclusion;

  return (
    <div className="space-y-6" data-testid="etr-eligibility-review">
      <section>
        <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-3">
          {t('eligibilityReview.heading')}
        </h2>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <div className="bg-brand-slate-50 rounded-card p-3 border border-brand-slate-200">
            <dt className="text-[11px] text-brand-slate-500 uppercase tracking-wide font-semibold">
              {t('eligibilityReview.statedCategory')}
            </dt>
            <dd className="text-sm font-medium text-brand-slate-800 mt-1">
              {data.statedCategory || '—'}
            </dd>
          </div>
          <div className="bg-brand-slate-50 rounded-card p-3 border border-brand-slate-200">
            <dt className="text-[11px] text-brand-slate-500 uppercase tracking-wide font-semibold">
              {t('eligibilityReview.statedConclusion')}
            </dt>
            <dd className="text-sm font-medium text-brand-slate-800 mt-1">
              {data.statedConclusion || '—'}
            </dd>
          </div>
        </div>

        <div
          className={`mt-4 rounded-card border p-4 flex items-start gap-3 ${
            supported
              ? 'bg-brand-teal-50 border-brand-teal-100'
              : 'bg-brand-danger-50 border-brand-danger-200'
          }`}
        >
          {supported ? (
            <CheckCircle2
              className="w-5 h-5 text-brand-teal-600 shrink-0 mt-0.5"
              strokeWidth={1.8}
              aria-hidden="true"
            />
          ) : (
            <XCircle
              className="w-5 h-5 text-brand-danger-700 shrink-0 mt-0.5"
              strokeWidth={1.8}
              aria-hidden="true"
            />
          )}
          <div className="flex-1">
            <div className="flex items-center gap-2 flex-wrap">
              <p
                className={`text-sm font-semibold ${
                  supported ? 'text-brand-teal-600' : 'text-brand-danger-700'
                }`}
              >
                {supported
                  ? t('eligibilityReview.dataSupports')
                  : t('eligibilityReview.dataDoesNotSupport')}
              </p>
              <Badge variant={supported ? 'success' : 'error'}>
                {supported ? t('eligibilityReview.supported') : t('eligibilityReview.notSupported')}
              </Badge>
            </div>
            {data.notes && (
              <p className="text-sm text-brand-slate-600 mt-1">{data.notes}</p>
            )}
          </div>
        </div>
      </section>

      <section className="space-y-5">
        <EvidenceList
          title={t('eligibilityReview.supportingEvidence', { count: data.supportingEvidence.length })}
          items={data.supportingEvidence}
          tone="support"
        />
        <EvidenceList
          title={t('eligibilityReview.contradictingEvidence', { count: data.contradictingEvidence.length })}
          items={data.contradictingEvidence}
          tone="contra"
        />
        <EvidenceList
          title={t('eligibilityReview.alternativeConsiderations', { count: data.alternativeConsiderations.length })}
          items={data.alternativeConsiderations}
          tone="neutral"
        />
      </section>
    </div>
  );
}
