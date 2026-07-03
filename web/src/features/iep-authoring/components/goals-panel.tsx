import { useState } from "react";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { useToast } from "@/components/ui/toast";
import { useEditorContext } from "../hooks/use-editor-context";
import { AddRowButton } from "./add-row-button";
import { GoalEditorRow } from "./goal-editor-row";
import { EmptyHint } from "./empty-hint";

export function GoalsPanel() {
  const { draftApi } = useEditorContext();
  const { show } = useToast();
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const goals = draftApi.draft?.goals ?? [];

  const confirmDelete = async () => {
    if (pendingDeleteId === null) return;
    setIsDeleting(true);
    if (await draftApi.removeGoal(pendingDeleteId)) {
      show({ message: "Goal deleted", variant: "success" });
    }
    setIsDeleting(false);
    setPendingDeleteId(null);
  };

  return (
    <section className="space-y-4" data-testid="goals-panel">
      {goals.length === 0 && (
        <EmptyHint>No goals yet. Add one to get started.</EmptyHint>
      )}
      {goals.map((goal) => (
        <GoalEditorRow
          key={goal.id}
          goal={goal}
          onDelete={setPendingDeleteId}
        />
      ))}
      <AddRowButton
        onClick={() => void draftApi.addGoal()}
        label="Add goal"
        testId="add-goal"
      />

      <ConfirmDialog
        open={pendingDeleteId !== null}
        title="Delete goal"
        message="Delete this goal? This cannot be undone."
        confirmLabel="Delete goal"
        loading={isDeleting}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDeleteId(null)}
        data-testid="goal-delete-dialog"
      />
    </section>
  );
}
