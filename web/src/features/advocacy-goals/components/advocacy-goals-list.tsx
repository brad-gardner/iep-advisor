import { useState } from "react";
import type { AdvocacyGoal } from "@/types/api";
import { Spinner } from "@/components/ui/spinner";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { useToast } from "@/components/ui/toast";
import {
  createAdvocacyGoal,
  updateAdvocacyGoal,
  deleteAdvocacyGoal,
  reorderAdvocacyGoals,
} from "../api/advocacy-goals-api";
import { AdvocacyGoalForm } from "./advocacy-goal-form";
import { AdvocacyGoalCard } from "./advocacy-goal-card";
import { AdvocacyGoalsEmptyState } from "./advocacy-goals-empty-state";

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
  // `formOpen` with a null `editingGoal` = add mode; a set goal = edit mode.
  const [formOpen, setFormOpen] = useState(false);
  const [editingGoal, setEditingGoal] = useState<AdvocacyGoal | null>(null);
  const [deletingGoal, setDeletingGoal] = useState<AdvocacyGoal | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const { show } = useToast();

  const openAdd = () => {
    setEditingGoal(null);
    setFormOpen(true);
  };
  const openEdit = (goal: AdvocacyGoal) => {
    setEditingGoal(goal);
    setFormOpen(true);
  };

  const handleCreate = async (data: {
    goalText: string;
    category?: string;
  }) => {
    try {
      const response = await createAdvocacyGoal(childId, data);
      if (response.success) {
        onReload();
        setFormOpen(false);
        show({ message: "Goal added", variant: "success" });
        return { success: true };
      }
      return {
        success: false,
        error: response.message || "Failed to create goal",
      };
    } catch {
      return { success: false, error: "An error occurred" };
    }
  };

  const handleUpdate = async (
    id: number,
    data: { goalText: string; category?: string },
  ) => {
    try {
      const response = await updateAdvocacyGoal(id, {
        goalText: data.goalText,
        category: data.category ?? "",
      });
      if (response.success) {
        onReload();
        setFormOpen(false);
        show({ message: "Goal updated", variant: "success" });
        return { success: true };
      }
      return {
        success: false,
        error: response.message || "Failed to update goal",
      };
    } catch {
      return { success: false, error: "An error occurred" };
    }
  };

  const confirmDelete = async () => {
    if (!deletingGoal) return;
    setIsDeleting(true);
    try {
      const response = await deleteAdvocacyGoal(deletingGoal.id);
      if (response.success) {
        onReload();
        setDeletingGoal(null);
        show({ message: "Goal removed", variant: "success" });
      }
    } catch {
      // handled by interceptor
    } finally {
      setIsDeleting(false);
    }
  };

  const handleReorder = async (goalId: number, direction: "up" | "down") => {
    const index = goals.findIndex((g) => g.id === goalId);
    if (index === -1) return;

    const swapIndex = direction === "up" ? index - 1 : index + 1;
    if (swapIndex < 0 || swapIndex >= goals.length) return;

    const reordered = [...goals];
    [reordered[index], reordered[swapIndex]] = [
      reordered[swapIndex],
      reordered[index],
    ];

    const items = reordered.map((g, i) => ({ id: g.id, displayOrder: i + 1 }));

    try {
      await reorderAdvocacyGoals(childId, { items });
      onReload();
    } catch {
      // handled by interceptor
    }
  };

  const goalFormModal = (
    <Modal
      open={formOpen}
      onClose={() => setFormOpen(false)}
      title={editingGoal ? "Edit goal" : "Add advocacy goal"}
      data-testid="goal-form-modal"
    >
      <AdvocacyGoalForm
        initialValues={
          editingGoal
            ? {
                goalText: editingGoal.goalText,
                category: editingGoal.category || "",
              }
            : undefined
        }
        onSubmit={
          editingGoal
            ? (data) => handleUpdate(editingGoal.id, data)
            : handleCreate
        }
        onCancel={() => setFormOpen(false)}
        submitLabel={editingGoal ? "Save Changes" : "Add Goal"}
      />
    </Modal>
  );

  if (isLoading) {
    return (
      <div className="flex justify-center py-6">
        <Spinner size="sm" />
      </div>
    );
  }

  if (goals.length === 0) {
    if (readOnly) {
      return (
        <p className="text-sm text-brand-slate-400 py-4 text-center">
          No advocacy goals have been set yet.
        </p>
      );
    }
    return (
      <>
        <AdvocacyGoalsEmptyState childName={childName} onAdd={openAdd} />
        {goalFormModal}
      </>
    );
  }

  return (
    <div className="space-y-3">
      <div className="flex justify-between items-center">
        <p
          className="text-[11px] text-brand-slate-400"
          data-testid="goal-count"
        >
          {goals.length}/10 goals
          {goals.length >= 10 &&
            " — Focused goals produce better analysis. Consider consolidating."}
        </p>
        {!readOnly && goals.length < 10 && (
          <Button
            variant="ghost"
            size="sm"
            onClick={openAdd}
            className="text-brand-teal-500 hover:bg-brand-teal-50"
            data-testid="add-goal-button"
          >
            + Add Goal
          </Button>
        )}
      </div>

      {goals.map((goal, index) => (
        <AdvocacyGoalCard
          key={goal.id}
          goal={goal}
          isFirst={index === 0}
          isLast={index === goals.length - 1}
          onMoveUp={readOnly ? undefined : () => handleReorder(goal.id, "up")}
          onMoveDown={
            readOnly ? undefined : () => handleReorder(goal.id, "down")
          }
          onEdit={readOnly ? undefined : () => openEdit(goal)}
          onDelete={readOnly ? undefined : () => setDeletingGoal(goal)}
        />
      ))}

      {goalFormModal}

      <ConfirmDialog
        open={deletingGoal !== null}
        title="Remove advocacy goal"
        message="Are you sure you want to remove this advocacy goal?"
        confirmLabel="Remove goal"
        loading={isDeleting}
        onConfirm={confirmDelete}
        onCancel={() => setDeletingGoal(null)}
        data-testid="goal-delete-dialog"
      />
    </div>
  );
}
