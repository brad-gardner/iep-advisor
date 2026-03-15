import type { SectionChanges } from '@/types/api';
import { Card } from '@/components/ui/card';

const SECTION_LABELS: Record<string, string> = {
  student_profile: 'Student Profile',
  present_levels: 'Present Levels of Performance',
  evaluations: 'Evaluations',
  assessments: 'Assessments & Test Scores',
  eligibility: 'Eligibility',
  annual_goals: 'Annual Goals',
  services: 'Services',
  accommodations: 'Accommodations',
  placement: 'Placement',
  transition: 'Transition Planning',
  progress_monitoring: 'Progress Monitoring',
  other: 'Other',
};

function SectionRow({
  sectionType,
  indicator,
}: {
  sectionType: string;
  indicator: '+' | '-' | '=';
}) {
  const indicatorStyles = {
    '+': 'text-brand-teal-600 bg-brand-teal-50',
    '-': 'text-brand-red bg-red-50',
    '=': 'text-brand-slate-400 bg-brand-slate-50',
  };

  return (
    <div className="flex items-center gap-3 py-1.5">
      <span
        className={`w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold shrink-0 ${indicatorStyles[indicator]}`}
      >
        {indicator === '=' ? '' : indicator}
      </span>
      <span className="text-sm text-brand-slate-700">
        {SECTION_LABELS[sectionType] || sectionType}
      </span>
    </div>
  );
}

export function SectionDiff({ changes }: { changes: SectionChanges }) {
  const hasChanges = changes.added.length > 0 || changes.removed.length > 0;

  if (!hasChanges && changes.inBoth.length === 0) {
    return null;
  }

  return (
    <Card>
      <h3 className="font-serif text-[17px] font-semibold text-brand-slate-800 mb-3">
        Section Changes
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
