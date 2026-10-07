import { AlertTriangle, CheckCircle2, XCircle, FileSearch } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { RedFlag } from '@/types/api';
import type { EtrCompletenessPayload, EtrEligibilityPayload } from '@/features/analysis/types';
import { RedFlagCard } from '@/features/iep-documents/components/red-flag-card';

interface EtrAnalysisOverviewProps {
  overallSummary: string;
  redFlags: RedFlag[];
  completeness: EtrCompletenessPayload | null;
  eligibility: EtrEligibilityPayload | null;
}

interface StatTileProps {
  label: string;
  value: React.ReactNode;
  Icon: typeof AlertTriangle;
  tone: 'teal' | 'amber' | 'red' | 'slate';
}

const TONE_CLASSES: Record<StatTileProps['tone'], { bg: string; border: string; icon: string; text: string }> = {
  teal: {
    bg: 'bg-brand-teal-50',
    border: 'border-brand-teal-100',
    icon: 'text-brand-teal-500',
    text: 'text-brand-teal-600',
  },
  amber: {
    bg: 'bg-brand-amber-50',
    border: 'border-brand-amber-100',
    icon: 'text-brand-amber-500',
    text: 'text-brand-amber-500',
  },
  red: {
    bg: 'bg-brand-danger-50',
    border: 'border-brand-danger-200',
    icon: 'text-brand-danger-700',
    text: 'text-brand-danger-700',
  },
  slate: {
    bg: 'bg-brand-slate-50',
    border: 'border-brand-slate-200',
    icon: 'text-brand-slate-500',
    text: 'text-brand-slate-800',
  },
};

function StatTile({ label, value, Icon, tone }: StatTileProps) {
  const c = TONE_CLASSES[tone];
  return (
    <div className={`rounded-card border p-3 ${c.bg} ${c.border}`}>
      <div className="flex items-center gap-2">
        <Icon className={`w-4 h-4 ${c.icon}`} strokeWidth={1.8} aria-hidden="true" />
        <span className="text-[11px] uppercase tracking-wide font-semibold text-brand-slate-500">
          {label}
        </span>
      </div>
      <div className={`mt-1 text-lg font-semibold ${c.text}`}>{value}</div>
    </div>
  );
}

export function EtrAnalysisOverview({
  overallSummary,
  redFlags,
  completeness,
  eligibility,
}: EtrAnalysisOverviewProps) {
  const { t } = useTranslation('etr-documents');
  const urgentFlagCount = redFlags.filter((f) => f.severity === 'red').length;
  const missingDomainCount = completeness?.missingDomains.length ?? 0;
  const supported = eligibility?.dataSupportsConclusion ?? null;

  return (
    <div className="space-y-6" data-testid="etr-analysis-overview">
      {overallSummary && (
        <section>
          <h2 className="font-serif text-[22px] font-semibold mb-3 text-brand-slate-800">
            {t('overview.heading')}
          </h2>
          <p className="text-brand-slate-600 text-sm leading-relaxed whitespace-pre-wrap">
            {overallSummary}
          </p>
        </section>
      )}

      <section className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <StatTile
          label={t('overview.redFlags')}
          value={
            <span>
              {redFlags.length}
              {urgentFlagCount > 0 && (
                <span className="text-[11px] font-normal text-brand-danger-700 ml-1.5">
                  {t('overview.urgentCount', { count: urgentFlagCount })}
                </span>
              )}
            </span>
          }
          Icon={AlertTriangle}
          tone={urgentFlagCount > 0 ? 'red' : redFlags.length > 0 ? 'amber' : 'slate'}
        />
        <StatTile
          label={t('overview.missingDomains')}
          value={missingDomainCount}
          Icon={FileSearch}
          tone={missingDomainCount > 0 ? 'amber' : 'slate'}
        />
        <StatTile
          label={t('overview.eligibility')}
          value={
            supported === null
              ? t('overview.unknown')
              : supported
                ? t('overview.supported')
                : t('overview.unsupported')
          }
          Icon={supported ? CheckCircle2 : XCircle}
          tone={supported === null ? 'slate' : supported ? 'teal' : 'red'}
        />
      </section>

      {redFlags.length > 0 && (
        <section>
          <h2 className="font-serif text-[22px] font-semibold mb-3 text-brand-slate-800">
            {t('overview.areasOfConcern', { count: redFlags.length })}
          </h2>
          <div className="space-y-3">
            {redFlags.map((flag, i) => (
              <RedFlagCard key={i} redFlag={flag} />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
