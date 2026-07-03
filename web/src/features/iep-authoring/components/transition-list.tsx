import { useState } from "react";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { useToast } from "@/components/ui/toast";
import { useEditorContext } from "../hooks/use-editor-context";
import { AddRowButton } from "./add-row-button";
import { EmptyHint } from "./empty-hint";
import { TransitionRow } from "./transition-row";

export function TransitionList() {
  const { draftApi } = useEditorContext();
  const { show } = useToast();
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const items = draftApi.draft?.transitionItems ?? [];

  const confirmDelete = async () => {
    if (pendingDeleteId === null) return;
    setIsDeleting(true);
    if (await draftApi.removeTransitionItem(pendingDeleteId)) {
      show({ message: "Transition item deleted", variant: "success" });
    }
    setIsDeleting(false);
    setPendingDeleteId(null);
  };

  return (
    <section className="space-y-4" data-testid="transition-list">
      {items.length === 0 && (
        <EmptyHint>No transition items yet. Add one to get started.</EmptyHint>
      )}
      {items.map((item) => (
        <TransitionRow
          key={item.id}
          item={item}
          onDelete={setPendingDeleteId}
        />
      ))}
      <AddRowButton
        onClick={() => void draftApi.addTransitionItem()}
        label="Add transition item"
        testId="add-transition"
      />

      <ConfirmDialog
        open={pendingDeleteId !== null}
        title="Delete transition item"
        message="Delete this transition item? This cannot be undone."
        confirmLabel="Delete item"
        loading={isDeleting}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDeleteId(null)}
        data-testid="transition-delete-dialog"
      />
    </section>
  );
}
