import { useState } from "react";
import { Link } from "react-router-dom";
import { Trash2, Eye, Download } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
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

function formatDate(dateStr: string | null): string {
  if (!dateStr) return "";
  return new Date(dateStr).toLocaleDateString();
}

function formatPeriod(start: string | null, end: string | null): string {
  if (!start && !end) return "Reporting period not set";
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
  const [deletingId, setDeletingId] = useState<number | null>(null);

  const handleDownload = async (id: number) => {
    const res = await getDownloadUrl(id);
    if (res.success && res.data) window.open(res.data.url, "_blank");
  };

  const handleDelete = async (id: number) => {
    if (!confirm("Delete this progress report?")) return;
    setDeletingId(id);
    try {
      const res = await remove(id);
      if (res.success) onChanged();
    } catch {
      // handled by interceptor
    } finally {
      setDeletingId(null);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-4">
        <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (reports.length === 0) {
    return (
      <p className="text-brand-slate-400 text-sm">
        No progress reports yet for this IEP.
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {reports.map((r) => (
        <Card
          key={r.id}
          className="p-3"
          data-testid="progress-report-card"
        >
          <div className="flex items-center justify-between">
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2 flex-wrap">
                <Link
                  to={`/children/${childId}/ieps/${iepId}/progress-reports/${r.id}`}
                  className="text-[13px] font-medium truncate text-brand-slate-800 hover:text-brand-teal-500 transition-colors"
                >
                  {r.fileName ||
                    formatPeriod(r.reportingPeriodStart, r.reportingPeriodEnd) ||
                    `Progress Report #${r.id}`}
                </Link>
                <Badge variant={STATUS_VARIANTS[r.status] || "neutral"}>
                  {r.status}
                </Badge>
              </div>
              <div className="flex gap-3 text-[11px] text-brand-slate-400 mt-1">
                <span>
                  {formatPeriod(r.reportingPeriodStart, r.reportingPeriodEnd)}
                </span>
                {r.fileSizeBytes > 0 && (
                  <span>{formatFileSize(r.fileSizeBytes)}</span>
                )}
                <span>Created {formatDate(r.createdAt)}</span>
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
                  View
                </Link>
              )}
              {r.fileSizeBytes > 0 && (
                <button
                  onClick={() => handleDownload(r.id)}
                  className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-slate-400 hover:text-brand-teal-500 transition-colors"
                >
                  <Download
                    className="w-3.5 h-3.5"
                    strokeWidth={1.8}
                    aria-hidden="true"
                  />
                  Download
                </button>
              )}
              {canEdit && (
                <button
                  onClick={() => handleDelete(r.id)}
                  disabled={deletingId === r.id}
                  className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-red hover:text-red-800 disabled:opacity-50 transition-colors"
                >
                  <Trash2
                    className="w-3.5 h-3.5"
                    strokeWidth={1.8}
                    aria-hidden="true"
                  />
                  {deletingId === r.id ? "..." : "Delete"}
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
    </div>
  );
}
