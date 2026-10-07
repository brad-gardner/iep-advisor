import { useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Trash2, Eye, Download } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Spinner } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { formatDate } from "@/lib/format-date";
import { documentStatusLabel } from "@/lib/document-status-label";
import i18n from "@/lib/i18n";
import { remove, getDownloadUrl } from "../api/progress-reports-api";
import { ProgressReportUpload } from "./progress-report-upload";
import type { ProgressReport } from "../types";

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

interface ProgressReportListProps {
  reports: ProgressReport[];
  isLoading: boolean;
  childId: number;
  iepId: number;
  canEdit: boolean;
  onChanged: () => void;
}

function formatPeriod(start: string | null, end: string | null): string {
  if (!start && !end) return i18n.t("progress-reports:list.periodNotSet");
  if (start && end) return `${formatDate(start)} – ${formatDate(end)}`;
  return formatDate(start || end);
}

function formatFileSize(bytes: number): string {
  if (bytes === 0) return "";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function ProgressReportList({
  reports,
  isLoading,
  childId,
  iepId,
  canEdit,
  onChanged,
}: ProgressReportListProps) {
  const { t } = useTranslation(["progress-reports", "iep-documents"]);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);
  const { show } = useToast();

  const handleDownload = async (id: number) => {
    const res = await getDownloadUrl(id);
    if (res.success && res.data) window.open(res.data.url, "_blank");
  };

  const confirmDelete = async () => {
    if (pendingDeleteId === null) return;
    const id = pendingDeleteId;
    setDeletingId(id);
    try {
      const res = await remove(id);
      if (res.success) {
        show({ message: t("list.deletedToast"), variant: "success" });
        setPendingDeleteId(null);
        onChanged();
      }
    } catch {
      // handled by interceptor
    } finally {
      setDeletingId(null);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-4">
        <Spinner size="sm" />
      </div>
    );
  }

  if (reports.length === 0) {
    return (
      <p className="text-brand-slate-500 text-sm">
        {t("list.empty")}
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {reports.map((r) => (
        <Card key={r.id} className="p-3" data-testid="progress-report-card">
          <div className="flex items-center justify-between">
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2 flex-wrap">
                <Link
                  to={`/children/${childId}/ieps/${iepId}/progress-reports/${r.id}`}
                  className="text-[13px] font-medium truncate text-brand-slate-800 hover:text-brand-teal-500 transition-colors"
                >
                  {r.fileName ||
                    formatPeriod(
                      r.reportingPeriodStart,
                      r.reportingPeriodEnd,
                    ) ||
                    t("list.idFallback", { id: r.id })}
                </Link>
                <Badge variant={STATUS_VARIANTS[r.status] || "neutral"}>
                  {documentStatusLabel(r.status)}
                </Badge>
              </div>
              <div className="flex gap-3 text-[11px] text-brand-slate-500 mt-1">
                <span>
                  {formatPeriod(r.reportingPeriodStart, r.reportingPeriodEnd)}
                </span>
                {r.fileSizeBytes > 0 && (
                  <span>{formatFileSize(r.fileSizeBytes)}</span>
                )}
                <span>{t("list.created", { date: formatDate(r.createdAt) })}</span>
              </div>
            </div>
            <div className="flex gap-2 ml-3 shrink-0">
              {r.status !== "created" && (
                <Link
                  to={`/children/${childId}/ieps/${iepId}/progress-reports/${r.id}`}
                  className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-teal-500 hover:text-brand-teal-600 transition-colors"
                >
                  <Eye
                    className="w-3.5 h-3.5"
                    strokeWidth={1.8}
                    aria-hidden="true"
                  />
                  {t("list.view")}
                </Link>
              )}
              {r.fileSizeBytes > 0 && (
                <button
                  onClick={() => handleDownload(r.id)}
                  className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-slate-500 hover:text-brand-teal-500 transition-colors"
                >
                  <Download
                    className="w-3.5 h-3.5"
                    strokeWidth={1.8}
                    aria-hidden="true"
                  />
                  {t("list.download")}
                </button>
              )}
              {canEdit && (
                <button
                  onClick={() => setPendingDeleteId(r.id)}
                  disabled={deletingId === r.id}
                  className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-danger-700 hover:text-brand-danger-800 disabled:opacity-50 transition-colors"
                >
                  <Trash2
                    className="w-3.5 h-3.5"
                    strokeWidth={1.8}
                    aria-hidden="true"
                  />
                  {deletingId === r.id ? t("list.deletingEllipsis") : t("list.delete")}
                </button>
              )}
            </div>
          </div>

          {canEdit && r.status === "created" && (
            <div className="mt-3">
              <ProgressReportUpload
                progressReportId={r.id}
                onUploaded={onChanged}
              />
            </div>
          )}
        </Card>
      ))}

      <ConfirmDialog
        open={pendingDeleteId !== null}
        title={t("list.deleteDialogTitle")}
        message={t("list.deleteDialogMessage")}
        confirmLabel={t("list.deleteConfirmLabel")}
        loading={deletingId !== null}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDeleteId(null)}
        data-testid="progress-report-delete-dialog"
      />
    </div>
  );
}
