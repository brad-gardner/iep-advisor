import type { SmartAnalysis } from '@/types/api';

interface SmartCriteriaGridProps {
  smartAnalysis: SmartAnalysis;
}

const RATING_COLORS: Record<string, string> = {
  green: 'bg-green-500',
  yellow: 'bg-yellow-500',
  red: 'bg-red-500',
};

const RATING_TEXT_COLORS: Record<string, string> = {
  green: 'text-green-600',
  yellow: 'text-yellow-600',
  red: 'text-red-600',
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
      <h4 className="text-sm font-medium text-gray-500 uppercase tracking-wide">
        SMART Analysis
      </h4>
      <div className="grid grid-cols-1 sm:grid-cols-5 gap-2">
        {CRITERIA_LABELS.map(({ key, label }) => {
          const criterion = smartAnalysis[key];
          return (
            <div key={key} className="bg-white rounded p-3 border border-gray-200">
              <div className="flex items-center gap-2 mb-1">
                <span
                  className={`inline-block w-2.5 h-2.5 rounded-full ${RATING_COLORS[criterion.rating] || 'bg-gray-400'}`}
                />
                <span className={`text-sm font-medium ${RATING_TEXT_COLORS[criterion.rating] || 'text-gray-500'}`}>
                  {label}
                </span>
              </div>
              <p className="text-xs text-gray-600 leading-relaxed">
                {criterion.explanation}
              </p>
            </div>
          );
        })}
      </div>
    </div>
  );
}
