import { useState } from "react";
import { Link } from "react-router-dom";
import { Trash2, Eye, FileSearch } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { remove as removeEtr } from "../api/etr-documents-api";
import {
  DOCUMENT_STATE_LABELS,
  EVALUATION_TYPE_LABELS,
  type EtrDocument,
} from "../types";

interface EtrDocumentListProps {
  etrs: EtrDocument[];
  isLoading: boolean;
  onDeleted: () => void;
}

const STATUS_VARIANTS: Record<
  string,
  "neutral" | "warning" | "success" | "error"
> = {
  created: "neutral",
  uploaded: "neutral",
  processing: "warning",
  parsed: "success",
  error: "error",
};

function formatDate(value: string | null): string {
  if (!value) return "";
  return new Date(value).toLocaleDateString();
}

export function EtrDocumentList({
  etrs,
  isLoading,
  onDeleted,
}: EtrDocumentListProps) {
  const { show: showToast } = useToast();
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);

  if (isLoading) {
    return (
      <div className="flex justify-center py-4">
        <Spinner size="sm" label="Loading ETR documents…" />
      </div>
    );
  }

  if (etrs.length === 0) {
    return <EmptyState icon={FileSearch} title="No ETR documents yet." />;
  }

  const confirmDelete = async () => {
    if (pendingDeleteId === null) return;
    const id = pendingDeleteId;
    setDeletingId(id);
    try {
      const response = await removeEtr(id);
      if (response.success) {
        showToast({ message: "ETR deleted", variant: "success" });
        setPendingDeleteId(null);
        onDeleted();
      }
    } catch {
      // handled by interceptor
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <div className="space-y-2">
      {etrs.map((etr) => {
        const title =
          etr.fileName ||
          (etr.evaluationType
            ? EVALUATION_TYPE_LABELS[etr.evaluationType] || etr.evaluationType
            : `ETR #${etr.id}`);

        return (
          <Card key={etr.id} className="p-3" data-testid="etr-document-card">
            <div className="flex items-center justify-between">
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2 flex-wrap">
                  <Link
                    to={`/children/${etr.childProfileId}/etrs/${etr.id}`}
                    className="text-[13px] font-medium truncate text-brand-slate-800 hover:text-brand-teal-500 transition-colors"
                    data-testid="etr-title-link"
                  >
                    {title}
                  </Link>
                  {etr.evaluationType && (
                    <Badge variant="neutral">
                      {EVALUATION_TYPE_LABELS[etr.evaluationType] ||
                        etr.evaluationType}
                    </Badge>
                  )}
                  {etr.documentState && (
                    <Badge
                      variant={
                        etr.documentState === "final" ? "success" : "neutral"
                      }
                    >
                      {DOCUMENT_STATE_LABELS[etr.documentState] ||
                        etr.documentState}
                    </Badge>
                  )}
                  <Badge variant={STATUS_VARIANTS[etr.status] || "neutral"}>
                    {etr.status}
                  </Badge>
                </div>
                <div className="flex gap-3 text-[11px] text-brand-slate-400 mt-1">
                  {etr.evaluationDate && (
                    <span>Evaluated: {formatDate(etr.evaluationDate)}</span>
                  )}
                  <span>Created {formatDate(etr.createdAt)}</span>
                </div>
              </div>
              <div className="flex gap-2 ml-3 shrink-0">
                <Link
                  to={`/children/${etr.childProfileId}/etrs/${etr.id}`}
                  className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-teal-500 hover:text-brand-teal-600 transition-colors"
                  data-testid="etr-view-link"
                >
                  <Eye
                    className="w-3.5 h-3.5"
                    strokeWidth={1.8}
                    aria-hidden="true"
                  />
                  View
                </Link>
                <Button
                  variant="danger"
                  size="sm"
                  onClick={() => setPendingDeleteId(etr.id)}
                  loading={deletingId === etr.id}
                  data-testid="etr-delete-button"
                >
                  <Trash2
                    className="w-3.5 h-3.5 mr-1"
                    strokeWidth={1.8}
                    aria-hidden="true"
                  />
                  Delete
                </Button>
              </div>
            </div>
          </Card>
        );
      })}

      <ConfirmDialog
        open={pendingDeleteId !== null}
        title="Delete ETR document"
        message="Delete this ETR document? This cannot be undone."
        confirmLabel="Delete ETR"
        loading={deletingId !== null}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDeleteId(null)}
        data-testid="etr-delete-dialog"
      />
    </div>
  );
}
