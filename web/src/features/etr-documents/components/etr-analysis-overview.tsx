import { AlertTriangle, CheckCircle2, XCircle, FileSearch } from 'lucide-react';
import type { ParsedEtrAnalysis } from '../lib/parse-analysis';

interface EtrAnalysisOverviewProps {
  parsed: ParsedEtrAnalysis;
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
    bg: 'bg-red-50',
    border: 'border-red-200',
    icon: 'text-brand-red',
    text: 'text-brand-red',
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

export function EtrAnalysisOverview({ parsed }: EtrAnalysisOverviewProps) {
  const { overallSummary, redFlags, assessmentCompleteness, eligibilityReview } = parsed;

  const highFlagCount = redFlags.filter((f) => f.severity === 'high').length;
  const missingDomainCount = assessmentCompleteness?.missing_domains.length ?? 0;
  const supported = eligibilityReview?.data_supports_conclusion ?? null;

  return (
    <div className="space-y-6" data-testid="etr-analysis-overview">
      {overallSummary && (
        <section>
          <h2 className="font-serif text-[22px] font-semibold mb-3 text-brand-slate-800">
            Overview
          </h2>
          <p className="text-brand-slate-600 text-sm leading-relaxed whitespace-pre-wrap">
            {overallSummary}
          </p>
        </section>
      )}

      <section className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <StatTile
          label="Red Flags"
          value={
            <span>
              {redFlags.length}
              {highFlagCount > 0 && (
                <span className="text-[11px] font-normal text-brand-red ml-1.5">
                  ({highFlagCount} high)
                </span>
              )}
            </span>
          }
          Icon={AlertTriangle}
          tone={highFlagCount > 0 ? 'red' : redFlags.length > 0 ? 'amber' : 'slate'}
        />
        <StatTile
          label="Missing Domains"
          value={missingDomainCount}
          Icon={FileSearch}
          tone={missingDomainCount > 0 ? 'amber' : 'slate'}
        />
        <StatTile
          label="Eligibility"
          value={
            supported === null
              ? '—'
              : supported
                ? 'Supported'
                : 'Unsupported'
          }
          Icon={supported ? CheckCircle2 : XCircle}
          tone={supported === null ? 'slate' : supported ? 'teal' : 'red'}
        />
      </section>
    </div>
  );
}
