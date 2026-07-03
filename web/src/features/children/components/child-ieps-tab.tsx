import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Drawer } from "@/components/ui/drawer";
import { useIepDocuments } from "@/features/iep-documents/hooks/use-iep-documents";
import { CreateIepForm } from "@/features/iep-documents/components/create-iep-form";
import { IepDocumentList } from "@/features/iep-documents/components/iep-document-list";
import type { ChildOutletContext } from "./child-detail-page";

export function ChildIepsTab() {
  const { child, childId, reloadChild } = useOutletContext<ChildOutletContext>();
  const [showCreateIep, setShowCreateIep] = useState(false);
  const {
    documents,
    isLoading: docsLoading,
    reload: reloadDocs,
  } = useIepDocuments(childId);
  const isViewer = child.role === "viewer";

  return (
    <Card data-testid="iep-documents-section">
      <div className="flex justify-between items-center mb-4">
        <h2 className="font-serif">IEPs</h2>
        {!isViewer && (
          <Button
            variant="secondary"
            onClick={() => setShowCreateIep(true)}
            data-testid="new-iep-button"
          >
            New IEP
          </Button>
        )}
      </div>
      <IepDocumentList
        documents={documents}
        isLoading={docsLoading}
        onDeleted={() => {
          reloadDocs();
          reloadChild();
        }}
        currentIepId={child.currentIepDocumentId}
        canSetCurrent={!isViewer}
        onCurrentChanged={reloadChild}
      />

      <Drawer
        open={showCreateIep}
        onClose={() => setShowCreateIep(false)}
        title="New IEP"
        size="lg"
        data-testid="new-iep-drawer"
      >
        <CreateIepForm
          childId={childId}
          onCreated={() => {
            setShowCreateIep(false);
            reloadDocs();
            reloadChild();
          }}
          onCancel={() => setShowCreateIep(false)}
        />
      </Drawer>
    </Card>
  );
}
