import type { GoalAlignment } from '@/types/api';
import { CATEGORY_COLORS } from '@/features/advocacy-goals/constants';

const STATUS_STYLES: Record<string, { badge: string; label: string }> = {
  addressed: {
    badge: 'bg-green-100 text-green-700',
    label: 'Addressed',
  },
  partially_addressed: {
    badge: 'bg-yellow-100 text-yellow-700',
    label: 'Partially Addressed',
  },
  not_addressed: {
    badge: 'bg-red-100 text-red-700',
    label: 'Not Addressed',
  },
};

interface GoalAlignmentCardProps {
  alignment: GoalAlignment;
}

export function GoalAlignmentCard({ alignment }: GoalAlignmentCardProps) {
  const status = STATUS_STYLES[alignment.alignmentStatus] || STATUS_STYLES.not_addressed;

  return (
    <div className="border border-gray-200 rounded-lg p-4 space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1">
          <div className="flex items-center gap-2 mb-1">
            {alignment.parentGoalCategory && (
              <span
                className={`text-xs px-2 py-0.5 rounded font-medium ${CATEGORY_COLORS[alignment.parentGoalCategory] || 'bg-gray-100 text-gray-600'}`}
              >
                {alignment.parentGoalCategory}
              </span>
            )}
            <span className={`text-xs px-2 py-0.5 rounded font-medium ${status.badge}`}>
              {status.label}
            </span>
          </div>
          <p className="text-sm font-medium text-gray-900">{alignment.parentGoalText}</p>
        </div>
      </div>

      <p className="text-sm text-gray-600">{alignment.explanation}</p>

      {alignment.alignedIepGoals.length > 0 && (
        <div>
          <p className="text-xs font-medium text-gray-500 mb-1">Aligned IEP Goals:</p>
          <ul className="space-y-1">
            {alignment.alignedIepGoals.map((goal, i) => (
              <li key={i} className="text-xs text-gray-600 bg-gray-50 rounded px-2 py-1">
                {goal}
              </li>
            ))}
          </ul>
        </div>
      )}

      {alignment.recommendation && (
        <div className="bg-blue-50 border border-blue-200 rounded p-3">
          <p className="text-xs font-medium text-blue-700 mb-1">Recommended Action:</p>
          <p className="text-sm text-blue-800">{alignment.recommendation}</p>
        </div>
      )}
    </div>
  );
}
