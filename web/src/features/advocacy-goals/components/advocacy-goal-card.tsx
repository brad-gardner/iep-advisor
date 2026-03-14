import type { AdvocacyGoal } from '@/types/api';
import { CATEGORY_COLORS } from '../constants';

interface AdvocacyGoalCardProps {
  goal: AdvocacyGoal;
  isFirst: boolean;
  isLast: boolean;
  onMoveUp: () => void;
  onMoveDown: () => void;
  onEdit: () => void;
  onDelete: () => void;
}

export function AdvocacyGoalCard({
  goal,
  isFirst,
  isLast,
  onMoveUp,
  onMoveDown,
  onEdit,
  onDelete,
}: AdvocacyGoalCardProps) {
  return (
    <div className="bg-gray-50 rounded-lg p-4 border border-gray-200 flex gap-3">
      <div className="flex flex-col gap-1 shrink-0">
        <button
          onClick={onMoveUp}
          disabled={isFirst}
          className="p-1 text-gray-400 hover:text-gray-700 disabled:opacity-30 transition-colors"
          aria-label="Move goal up"
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 15l7-7 7 7" />
          </svg>
        </button>
        <button
          onClick={onMoveDown}
          disabled={isLast}
          className="p-1 text-gray-400 hover:text-gray-700 disabled:opacity-30 transition-colors"
          aria-label="Move goal down"
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
          </svg>
        </button>
      </div>

      <div className="flex-1 min-w-0">
        <div className="flex items-start gap-2 mb-1">
          {goal.category && (
            <span
              className={`inline-block px-2 py-0.5 rounded text-xs font-medium shrink-0 ${CATEGORY_COLORS[goal.category] || 'bg-gray-100 text-gray-600'}`}
            >
              {goal.category}
            </span>
          )}
        </div>
        <p className="text-sm text-gray-900">{goal.goalText}</p>
      </div>

      <div className="flex gap-1 shrink-0">
        <button
          onClick={onEdit}
          className="p-1.5 text-gray-400 hover:text-blue-600 transition-colors"
          aria-label="Edit goal"
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"
            />
          </svg>
        </button>
        <button
          onClick={onDelete}
          className="p-1.5 text-gray-400 hover:text-red-600 transition-colors"
          aria-label="Delete goal"
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
            />
          </svg>
        </button>
      </div>
    </div>
  );
}
