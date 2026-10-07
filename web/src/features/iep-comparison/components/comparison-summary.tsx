import { useTranslation } from 'react-i18next';
import type { ComparisonSummary as ComparisonSummaryType } from '@/types/api';

function StatBox({
  label,
  value,
  variant,
}: {
  label: string;
  value: number;
  variant: 'teal' | 'amber' | 'red' | 'neutral';
}) {
  const colorMap = {
    teal: 'bg-brand-teal-50 text-brand-teal-600 border-brand-teal-100',
    amber: 'bg-brand-amber-50 text-brand-amber-500 border-brand-amber-100',
    red: 'bg-brand-danger-50 text-brand-danger-700 border-brand-danger-200',
    neutral: 'bg-brand-slate-50 text-brand-slate-600 border-brand-slate-200',
  };

  return (
    <div
      className={`rounded-card border px-3 py-2 text-center ${colorMap[variant]}`}
    >
      <p className="text-xl font-semibold">{value}</p>
      <p className="text-[11px] uppercase tracking-wide font-medium mt-0.5">{label}</p>
    </div>
  );
}

export function ComparisonSummary({ summary }: { summary: ComparisonSummaryType }) {
  const { t } = useTranslation('iep-comparison');
  return (
    <div className="grid grid-cols-3 sm:grid-cols-4 lg:grid-cols-9 gap-2">
      <StatBox label={t('summary.goalsAdded')} value={summary.goalsAdded} variant="teal" />
      <StatBox label={t('summary.goalsRemoved')} value={summary.goalsRemoved} variant="red" />
      <StatBox label={t('summary.goalsModified')} value={summary.goalsModified} variant="amber" />
      <StatBox label={t('summary.goalsUnchanged')} value={summary.goalsUnchanged} variant="neutral" />
      <StatBox label={t('summary.sectionsAdded')} value={summary.sectionsAdded} variant="teal" />
      <StatBox label={t('summary.sectionsRemoved')} value={summary.sectionsRemoved} variant="red" />
      <StatBox label={t('summary.flagsResolved')} value={summary.redFlagsResolved} variant="teal" />
      <StatBox label={t('summary.flagsPersisting')} value={summary.redFlagsPersisting} variant="amber" />
      <StatBox label={t('summary.newFlags')} value={summary.newRedFlags} variant="red" />
    </div>
  );
}
