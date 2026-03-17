import { ChevronUp, ChevronDown, Pencil, Trash2 } from 'lucide-react';
import type { AdvocacyGoal } from '@/types/api';
import { Badge } from '@/components/ui/badge';

interface AdvocacyGoalCardProps {
  goal: AdvocacyGoal;
  isFirst: boolean;
  isLast: boolean;
  onMoveUp?: () => void;
  onMoveDown?: () => void;
  onEdit?: () => void;
  onDelete?: () => void;
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
    <div className="bg-white rounded-card p-4 border-[0.5px] border-brand-slate-200 flex gap-3" data-testid="goal-card">
      {(onMoveUp || onMoveDown) && (
        <div className="flex flex-col gap-1 shrink-0">
          <button
            onClick={onMoveUp}
            disabled={isFirst}
            className="p-1 text-brand-slate-300 hover:text-brand-slate-600 disabled:opacity-30 transition-colors"
            data-testid="goal-move-up"
            aria-label="Move goal up"
          >
            <ChevronUp size={16} strokeWidth={1.8} aria-hidden="true" />
          </button>
          <button
            onClick={onMoveDown}
            disabled={isLast}
            className="p-1 text-brand-slate-300 hover:text-brand-slate-600 disabled:opacity-30 transition-colors"
            data-testid="goal-move-down"
            aria-label="Move goal down"
          >
            <ChevronDown size={16} strokeWidth={1.8} aria-hidden="true" />
          </button>
        </div>
      )}

      <div className="flex-1 min-w-0">
        {goal.category && (
          <div className="mb-1">
            <Badge variant={goal.category === 'academic' || goal.category === 'services' ? 'success' : goal.category === 'behavioral' ? 'warning' : 'neutral'}>
              {goal.category}
            </Badge>
          </div>
        )}
        <p className="text-sm text-brand-slate-800">{goal.goalText}</p>
      </div>

      {(onEdit || onDelete) && (
        <div className="flex gap-1 shrink-0">
          {onEdit && (
            <button
              onClick={onEdit}
              className="p-1.5 text-brand-slate-300 hover:text-brand-teal-500 transition-colors"
              data-testid="goal-edit"
              aria-label="Edit goal"
            >
              <Pencil size={16} strokeWidth={1.8} aria-hidden="true" />
            </button>
          )}
          {onDelete && (
            <button
              onClick={onDelete}
              className="p-1.5 text-brand-slate-300 hover:text-brand-red transition-colors"
              data-testid="goal-delete"
              aria-label="Delete goal"
            >
              <Trash2 size={16} strokeWidth={1.8} aria-hidden="true" />
            </button>
          )}
        </div>
      )}
    </div>
  );
}
