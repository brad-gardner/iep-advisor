import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useEtrDocuments } from "@/features/etr-documents/hooks/use-etr-documents";
import { CreateEtrForm } from "@/features/etr-documents/components/create-etr-form";
import { EtrDocumentList } from "@/features/etr-documents/components/etr-document-list";
import type { ChildOutletContext } from "./child-detail-page";

export function ChildEtrsTab() {
  const { child, childId } = useOutletContext<ChildOutletContext>();
  const [showCreateEtr, setShowCreateEtr] = useState(false);
  const {
    etrs,
    isLoading: etrsLoading,
    reload: reloadEtrs,
  } = useEtrDocuments(childId);
  const isViewer = child.role === "viewer";

  return (
    <Card data-testid="etr-documents-section">
      <div className="flex justify-between items-center mb-4">
        <h2 className="font-serif">ETRs</h2>
        {!isViewer && !showCreateEtr && (
          <Button
            variant="secondary"
            onClick={() => setShowCreateEtr(true)}
            data-testid="new-etr-button"
          >
            New ETR
          </Button>
        )}
      </div>
      {!isViewer && showCreateEtr && (
        <div className="mb-4">
          <CreateEtrForm
            childId={childId}
            onCreated={() => {
              setShowCreateEtr(false);
              reloadEtrs();
            }}
            onCancel={() => setShowCreateEtr(false)}
          />
        </div>
      )}
      <EtrDocumentList
        etrs={etrs}
        isLoading={etrsLoading}
        onDeleted={reloadEtrs}
      />
    </Card>
  );
}
