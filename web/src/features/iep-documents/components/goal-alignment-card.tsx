import { Lightbulb } from 'lucide-react';
import type { GoalAlignment } from '@/types/api';
import { CATEGORY_COLORS } from '@/features/advocacy-goals/constants';
import { Badge } from '@/components/ui/badge';

const STATUS_VARIANTS: Record<string, { variant: 'success' | 'warning' | 'error'; label: string }> = {
  addressed: { variant: 'success', label: 'Addressed' },
  partially_addressed: { variant: 'warning', label: 'Partially Addressed' },
  not_addressed: { variant: 'error', label: 'Not Addressed' },
};

interface GoalAlignmentCardProps {
  alignment: GoalAlignment;
}

export function GoalAlignmentCard({ alignment }: GoalAlignmentCardProps) {
  const status = STATUS_VARIANTS[alignment.alignmentStatus] || STATUS_VARIANTS.not_addressed;

  return (
    <div className="border-[0.5px] border-brand-slate-200 rounded-card p-4 space-y-3 bg-white">
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1">
          <div className="flex items-center gap-2 mb-1">
            {alignment.parentGoalCategory && (
              <span
                className={`text-xs px-2 py-0.5 rounded-badge font-medium ${CATEGORY_COLORS[alignment.parentGoalCategory] || 'bg-brand-slate-50 text-brand-slate-600'}`}
              >
                {alignment.parentGoalCategory}
              </span>
            )}
            <Badge variant={status.variant}>
              {status.label}
            </Badge>
          </div>
          <p className="text-sm font-medium text-brand-slate-800">{alignment.parentGoalText}</p>
        </div>
      </div>

      <p className="text-sm text-brand-slate-600">{alignment.explanation}</p>

      {alignment.alignedIepGoals.length > 0 && (
        <div>
          <p className="text-[10px] font-semibold text-brand-teal-500 uppercase tracking-wide mb-1">
            Aligned IEP Goals
          </p>
          <ul className="space-y-1">
            {alignment.alignedIepGoals.map((goal, i) => (
              <li key={i} className="text-[11px] text-brand-slate-600 bg-brand-slate-50 rounded-badge px-2 py-1">
                {goal}
              </li>
            ))}
          </ul>
        </div>
      )}

      {alignment.recommendation && (
        <div className="border-l-2 border-l-brand-teal-500 bg-brand-teal-50 rounded-r-card p-3">
          <div className="flex items-center gap-1.5 mb-1">
            <Lightbulb className="w-3.5 h-3.5 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
            <p className="text-[11px] font-medium text-brand-teal-600">Recommended Action</p>
          </div>
          <p className="text-sm text-brand-slate-600">{alignment.recommendation}</p>
        </div>
      )}
    </div>
  );
}
