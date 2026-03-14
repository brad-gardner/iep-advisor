import type { SmartAnalysis } from '@/types/api';

interface SmartCriteriaGridProps {
  smartAnalysis: SmartAnalysis;
}

const RATING_COLORS: Record<string, string> = {
  green: 'bg-brand-teal-500',
  yellow: 'bg-brand-amber-400',
  red: 'bg-brand-red',
};

const RATING_TEXT_COLORS: Record<string, string> = {
  green: 'text-brand-teal-600',
  yellow: 'text-brand-amber-500',
  red: 'text-brand-red',
};

const CRITERIA_LABELS: { key: keyof SmartAnalysis; label: string }[] = [
  { key: 'specific', label: 'Specific' },
  { key: 'measurable', label: 'Measurable' },
  { key: 'achievable', label: 'Achievable' },
  { key: 'relevant', label: 'Relevant' },
  { key: 'timeBound', label: 'Time-bound' },
];

export function SmartCriteriaGrid({ smartAnalysis }: SmartCriteriaGridProps) {
  return (
    <div className="space-y-2">
      <h4 className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide">
        SMART Analysis
      </h4>
      <div className="grid grid-cols-1 sm:grid-cols-5 gap-2">
        {CRITERIA_LABELS.map(({ key, label }) => {
          const criterion = smartAnalysis[key];
          return (
            <div key={key} className="bg-white rounded-card p-3 border-[0.5px] border-brand-slate-200">
              <div className="flex items-center gap-2 mb-1">
                <span
                  className={`inline-block w-2.5 h-2.5 rounded-full ${RATING_COLORS[criterion.rating] || 'bg-brand-slate-400'}`}
                />
                <span className={`text-[13px] font-medium ${RATING_TEXT_COLORS[criterion.rating] || 'text-brand-slate-400'}`}>
                  {label}
                </span>
              </div>
              <p className="text-[11px] text-brand-slate-600 leading-relaxed">
                {criterion.explanation}
              </p>
            </div>
          );
        })}
      </div>
    </div>
  );
}
