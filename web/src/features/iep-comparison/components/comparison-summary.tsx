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
    red: 'bg-red-50 text-brand-red border-red-200',
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
  return (
    <div className="grid grid-cols-3 sm:grid-cols-4 lg:grid-cols-9 gap-2">
      <StatBox label="Goals Added" value={summary.goalsAdded} variant="teal" />
      <StatBox label="Goals Removed" value={summary.goalsRemoved} variant="red" />
      <StatBox label="Goals Modified" value={summary.goalsModified} variant="amber" />
      <StatBox label="Goals Unchanged" value={summary.goalsUnchanged} variant="neutral" />
      <StatBox label="Sections Added" value={summary.sectionsAdded} variant="teal" />
      <StatBox label="Sections Removed" value={summary.sectionsRemoved} variant="red" />
      <StatBox label="Flags Resolved" value={summary.redFlagsResolved} variant="teal" />
      <StatBox label="Flags Persisting" value={summary.redFlagsPersisting} variant="amber" />
      <StatBox label="New Flags" value={summary.newRedFlags} variant="red" />
    </div>
  );
}
