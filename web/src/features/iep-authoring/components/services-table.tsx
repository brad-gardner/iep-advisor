import { useState } from "react";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { useToast } from "@/components/ui/toast";
import { useEditorContext } from "../hooks/use-editor-context";
import { AddRowButton } from "./add-row-button";
import { EmptyHint } from "./empty-hint";
import { ServiceLineRow } from "./service-line-row";

export function ServicesTable() {
  const { draftApi } = useEditorContext();
  const { show } = useToast();
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const services = draftApi.draft?.serviceLines ?? [];

  const confirmDelete = async () => {
    if (pendingDeleteId === null) return;
    setIsDeleting(true);
    if (await draftApi.removeServiceLine(pendingDeleteId)) {
      show({ message: "Service deleted", variant: "success" });
    }
    setIsDeleting(false);
    setPendingDeleteId(null);
  };

  return (
    <section className="space-y-4" data-testid="services-table">
      {services.length === 0 && (
        <EmptyHint>No services yet. Add one to get started.</EmptyHint>
      )}
      {services.map((service) => (
        <ServiceLineRow
          key={service.id}
          service={service}
          onDelete={setPendingDeleteId}
        />
      ))}
      <AddRowButton
        onClick={() => void draftApi.addServiceLine()}
        label="Add service"
        testId="add-service"
      />

      <ConfirmDialog
        open={pendingDeleteId !== null}
        title="Delete service"
        message="Delete this service? This cannot be undone."
        confirmLabel="Delete service"
        loading={isDeleting}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDeleteId(null)}
        data-testid="service-delete-dialog"
      />
    </section>
  );
}
