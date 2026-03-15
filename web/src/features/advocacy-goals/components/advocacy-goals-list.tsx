import { useState } from 'react';
import type { AdvocacyGoal } from '@/types/api';
import {
  createAdvocacyGoal,
  updateAdvocacyGoal,
  deleteAdvocacyGoal,
  reorderAdvocacyGoals,
} from '../api/advocacy-goals-api';
import { AdvocacyGoalForm } from './advocacy-goal-form';
import { AdvocacyGoalCard } from './advocacy-goal-card';
import { AdvocacyGoalsEmptyState } from './advocacy-goals-empty-state';

interface AdvocacyGoalsListProps {
  childId: number;
  childName: string;
  goals: AdvocacyGoal[];
  isLoading: boolean;
  onReload: () => void;
  readOnly?: boolean;
}

export function AdvocacyGoalsList({
  childId,
  childName,
  goals,
  isLoading,
  onReload,
  readOnly = false,
}: AdvocacyGoalsListProps) {
  const [isAdding, setIsAdding] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);

  const handleCreate = async (data: { goalText: string; category?: string }) => {
    try {
      const response = await createAdvocacyGoal(childId, data);
      if (response.success) {
        onReload();
        setIsAdding(false);
        return { success: true };
      }
      return { success: false, error: response.message || 'Failed to create goal' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  const handleUpdate = async (id: number, data: { goalText: string; category?: string }) => {
    try {
      const response = await updateAdvocacyGoal(id, {
        goalText: data.goalText,
        category: data.category ?? '',
      });
      if (response.success) {
        onReload();
        setEditingId(null);
        return { success: true };
      }
      return { success: false, error: response.message || 'Failed to update goal' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  const handleDelete = async (id: number) => {
    if (!confirm('Are you sure you want to remove this advocacy goal?')) return;
    try {
      const response = await deleteAdvocacyGoal(id);
      if (response.success) {
        onReload();
      }
    } catch {
      // handled by interceptor
    }
  };

  const handleReorder = async (goalId: number, direction: 'up' | 'down') => {
    const index = goals.findIndex((g) => g.id === goalId);
    if (index === -1) return;

    const swapIndex = direction === 'up' ? index - 1 : index + 1;
    if (swapIndex < 0 || swapIndex >= goals.length) return;

    const reordered = [...goals];
    [reordered[index], reordered[swapIndex]] = [reordered[swapIndex], reordered[index]];

    const items = reordered.map((g, i) => ({ id: g.id, displayOrder: i + 1 }));

    try {
      await reorderAdvocacyGoals(childId, { items });
      onReload();
    } catch {
      // handled by interceptor
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-6">
        <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (goals.length === 0 && !isAdding) {
    if (readOnly) {
      return (
        <p className="text-sm text-brand-slate-400 py-4 text-center">
          No advocacy goals have been set yet.
        </p>
      );
    }
    return <AdvocacyGoalsEmptyState childName={childName} onAdd={() => setIsAdding(true)} />;
  }

  return (
    <div className="space-y-3">
      <div className="flex justify-between items-center">
        <p className="text-[11px] text-brand-slate-400">
          {goals.length}/10 goals
          {goals.length >= 10 && ' — Focused goals produce better analysis. Consider consolidating.'}
        </p>
        {!readOnly && !isAdding && goals.length < 10 && (
          <button
            onClick={() => setIsAdding(true)}
            className="text-sm text-brand-teal-500 hover:text-brand-teal-600 transition-colors"
          >
            + Add Goal
          </button>
        )}
      </div>

      {!readOnly && isAdding && (
        <div className="bg-white rounded-card p-4 border-[0.5px] border-brand-teal-200">
          <AdvocacyGoalForm
            onSubmit={handleCreate}
            onCancel={() => setIsAdding(false)}
            submitLabel="Add Goal"
          />
        </div>
      )}

      {goals.map((goal, index) =>
        !readOnly && editingId === goal.id ? (
          <div key={goal.id} className="bg-white rounded-card p-4 border-[0.5px] border-brand-teal-200">
            <AdvocacyGoalForm
              initialValues={{ goalText: goal.goalText, category: goal.category || '' }}
              onSubmit={(data) => handleUpdate(goal.id, data)}
              onCancel={() => setEditingId(null)}
              submitLabel="Save Changes"
            />
          </div>
        ) : (
          <AdvocacyGoalCard
            key={goal.id}
            goal={goal}
            isFirst={index === 0}
            isLast={index === goals.length - 1}
            onMoveUp={readOnly ? undefined : () => handleReorder(goal.id, 'up')}
            onMoveDown={readOnly ? undefined : () => handleReorder(goal.id, 'down')}
            onEdit={readOnly ? undefined : () => setEditingId(goal.id)}
            onDelete={readOnly ? undefined : () => handleDelete(goal.id)}
          />
        )
      )}
    </div>
  );
}
