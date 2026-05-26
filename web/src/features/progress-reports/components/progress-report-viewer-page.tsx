import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Notice } from "@/components/ui/notice";
import { PdfViewer } from "@/components/ui/pdf-viewer";
import { getById, getDownloadUrl } from "../api/progress-reports-api";
import { ProgressReportAnalysisTab } from "./progress-report-analysis-tab";
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

function formatDate(dateStr: string | null): string {
  if (!dateStr) return "";
  return new Date(dateStr).toLocaleDateString();
}

function formatPeriod(
  start: string | null,
  end: string | null
): string {
  if (!start && !end) return "Reporting period not set";
  if (start && end) return `${formatDate(start)} – ${formatDate(end)}`;
  return formatDate(start || end);
}

export function ProgressReportViewerPage() {
  const { childId, id, prId } = useParams<{
    childId: string;
    id: string;
    prId: string;
  }>();
  const reportId = Number(prId);
  const [report, setReport] = useState<ProgressReport | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<"document" | "analysis">("document");

  useEffect(() => {
    let cancelled = false;
    setIsLoading(true);
    getById(reportId)
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) setReport(res.data);
      })
      .catch(() => {})
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [reportId]);

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (!report) {
    return (
      <div className="text-center py-12">
        <p className="text-brand-slate-400">Progress report not found.</p>
        <Link
          to={`/children/${childId}/ieps/${id}`}
          className="text-brand-teal-500 hover:underline mt-2 inline-block"
        >
          Back to IEP
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div>
        <Link
          to={`/children/${childId}/ieps/${id}`}
          className="inline-flex items-center gap-1.5 text-[13px] font-medium text-brand-slate-400 hover:text-brand-teal-500 transition-colors"
        >
          <ArrowLeft
            className="w-4 h-4"
            strokeWidth={1.8}
            aria-hidden="true"
          />
          Back to IEP
        </Link>
        <h1 className="font-serif text-[32px] font-semibold leading-tight mt-1 text-brand-slate-800">
          {report.fileName ||
            formatPeriod(
              report.reportingPeriodStart,
              report.reportingPeriodEnd
            ) ||
            `Progress Report #${report.id}`}
        </h1>
        <div className="flex items-center gap-3 mt-2 flex-wrap">
          <Badge variant={STATUS_VARIANTS[report.status] || "neutral"}>
            {report.status}
          </Badge>
          <span className="text-[13px] text-brand-slate-500">
            {formatPeriod(
              report.reportingPeriodStart,
              report.reportingPeriodEnd
            )}
          </span>
        </div>
        {report.notes && (
          <p className="mt-3 text-sm text-brand-slate-600 whitespace-pre-wrap bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200">
            {report.notes}
          </p>
        )}
      </div>

      {report.status === "error" && report.errorMessage && (
        <Notice variant="error" title="Processing failed">
          {report.errorMessage}
        </Notice>
      )}

      {report.status === "created" && (
        <Notice variant="info" title="No file attached">
          Upload the progress report PDF from the IEP's Progress Reports tab to
          continue.
        </Notice>
      )}

      {report.status !== "created" && (
        <>
          <div className="flex border-b border-brand-slate-200">
            <button
              onClick={() => setActiveTab("document")}
              data-testid="pr-tab-document"
              className={`px-4 py-2 text-[13px] font-medium transition-colors ${
                activeTab === "document"
                  ? "text-brand-slate-800 border-b-2 border-brand-teal-500"
                  : "text-brand-slate-400 hover:text-brand-slate-800"
              }`}
            >
              Document
            </button>
            <button
              onClick={() => setActiveTab("analysis")}
              data-testid="pr-tab-analysis"
              className={`px-4 py-2 text-[13px] font-medium transition-colors ${
                activeTab === "analysis"
                  ? "text-brand-slate-800 border-b-2 border-brand-teal-500"
                  : "text-brand-slate-400 hover:text-brand-slate-800"
              }`}
            >
              Analysis
            </button>
          </div>

          {activeTab === "document" && (
            <PdfViewer
              fileName={report.fileName}
              loadUrl={async () => {
                const res = await getDownloadUrl(report.id);
                return res.success && res.data ? res.data.url : null;
              }}
            />
          )}

          {activeTab === "analysis" && (
            <ProgressReportAnalysisTab progressReportId={report.id} />
          )}
        </>
      )}
    </div>
  );
}
