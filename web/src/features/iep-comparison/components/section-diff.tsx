import { useTranslation } from 'react-i18next';
import type { SectionChanges } from '@/types/api';
import { Card } from '@/components/ui/card';
import { sectionTypeLabel } from '@/lib/section-type-label';

function SectionRow({
  sectionType,
  indicator,
}: {
  sectionType: string;
  indicator: '+' | '-' | '=';
}) {
  const indicatorStyles = {
    '+': 'text-brand-teal-600 bg-brand-teal-50',
    '-': 'text-brand-danger-700 bg-brand-danger-50',
    '=': 'text-brand-slate-400 bg-brand-slate-50',
  };
  // Computed outside the JSX expression so the 'full' style argument
  // doesn't trip `i18next/no-literal-string` (jsx-only mode still flags a
  // literal nested inside a JSX child's expression).
  const label = sectionTypeLabel(sectionType, 'full');

  return (
    <div className="flex items-center gap-3 py-1.5">
      <span
        className={`w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold shrink-0 ${indicatorStyles[indicator]}`}
      >
        {indicator === '=' ? '' : indicator}
      </span>
      <span className="text-sm text-brand-slate-700">
        {label}
      </span>
    </div>
  );
}

export function SectionDiff({ changes }: { changes: SectionChanges }) {
  const { t } = useTranslation(['iep-comparison', 'iep-documents']);
  const hasChanges = changes.added.length > 0 || changes.removed.length > 0;

  if (!hasChanges && changes.inBoth.length === 0) {
    return null;
  }

  return (
    <Card>
      <h3 className="font-serif text-[17px] font-semibold text-brand-slate-800 mb-3">
        {t('sectionDiff.heading')}
      </h3>

      <div className="divide-y divide-brand-slate-100">
        {changes.added.map((s) => (
          <SectionRow key={s} sectionType={s} indicator="+" />
        ))}
        {changes.removed.map((s) => (
          <SectionRow key={s} sectionType={s} indicator="-" />
        ))}
        {changes.inBoth.map((s) => (
          <SectionRow key={s} sectionType={s} indicator="=" />
        ))}
      </div>
    </Card>
  );
}
