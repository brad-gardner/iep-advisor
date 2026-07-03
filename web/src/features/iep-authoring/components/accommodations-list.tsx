import { useState } from "react";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { useToast } from "@/components/ui/toast";
import { useEditorContext } from "../hooks/use-editor-context";
import { AccommodationRow } from "./accommodation-row";
import { AddRowButton } from "./add-row-button";
import { EmptyHint } from "./empty-hint";

export function AccommodationsList() {
  const { draftApi } = useEditorContext();
  const { show } = useToast();
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const accommodations = draftApi.draft?.accommodations ?? [];

  const confirmDelete = async () => {
    if (pendingDeleteId === null) return;
    setIsDeleting(true);
    if (await draftApi.removeAccommodation(pendingDeleteId)) {
      show({ message: "Accommodation deleted", variant: "success" });
    }
    setIsDeleting(false);
    setPendingDeleteId(null);
  };

  return (
    <section className="space-y-4" data-testid="accommodations-list">
      {accommodations.length === 0 && (
        <EmptyHint>No accommodations yet. Add one to get started.</EmptyHint>
      )}
      {accommodations.map((accommodation) => (
        <AccommodationRow
          key={accommodation.id}
          accommodation={accommodation}
          onDelete={setPendingDeleteId}
        />
      ))}
      <AddRowButton
        onClick={() => void draftApi.addAccommodation()}
        label="Add accommodation"
        testId="add-accommodation"
      />

      <ConfirmDialog
        open={pendingDeleteId !== null}
        title="Delete accommodation"
        message="Delete this accommodation? This cannot be undone."
        confirmLabel="Delete accommodation"
        loading={isDeleting}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDeleteId(null)}
        data-testid="accommodation-delete-dialog"
      />
    </section>
  );
}
