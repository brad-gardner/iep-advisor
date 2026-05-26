import { useEffect, useState, useRef, useCallback } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import {
  ArrowLeft,
  ChevronDown,
  ChevronUp,
  Download,
  Play,
  ArrowRightLeft,
} from "lucide-react";
import type { IepDocument, IepSection } from "@/types/api";
import {
  getIepDocument,
  getIepSections,
  getDownloadUrl,
  reprocessIep,
  getIepDocuments,
} from "../api/iep-documents-api";
import { getChild, setCurrentIep } from "@/features/children/api/children-api";
import { usePolling } from "@/hooks/use-polling";
import { Badge } from "@/components/ui/badge";
import { useIepAnalysis } from "../hooks/use-iep-analysis";
import { useAdvocacyGoals } from "@/features/advocacy-goals/hooks/use-advocacy-goals";
import { useMeetingPrep } from "@/features/meeting-prep/hooks/use-meeting-prep";
import { AnalysisTab } from "./analysis-tab";
import { MeetingPrepTab } from "@/features/meeting-prep/components/meeting-prep-tab";
import { Button } from "@/components/ui/button";
import { Notice } from "@/components/ui/notice";
import { PdfViewer } from "@/components/ui/pdf-viewer";
import { ProgressReportsTab } from "@/features/progress-reports/components/progress-reports-tab";

const MEETING_TYPE_LABELS: Record<string, string> = {
  initial: "Initial IEP",
  annual_review: "Annual Review",
  amendment: "Amendment",
  reevaluation: "Reevaluation",
};

export function IepViewerPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const documentId = Number(id);
  const [document, setDocument] = useState<IepDocument | null>(null);
  const [sections, setSections] = useState<IepSection[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<
    "document" | "analysis" | "meeting-prep" | "progress-reports"
  >("document");
  const [notesExpanded, setNotesExpanded] = useState(false);
  const [compareOpen, setCompareOpen] = useState(false);
  const [otherIeps, setOtherIeps] = useState<IepDocument[]>([]);
  const [childCurrentIepId, setChildCurrentIepId] = useState<number | null>(null);
  const [childRole, setChildRole] = useState<string | null>(null);
  const [settingCurrent, setSettingCurrent] = useState(false);
  const compareRef = useRef<HTMLDivElement>(null);

  const {
    analysis,
    isLoading: analysisLoading,
    isTriggering,
    trigger: triggerAnalysis,
    reload: reloadAnalysis,
  } = useIepAnalysis(documentId);

  const { goals: advocacyGoals } = useAdvocacyGoals(
    document?.childProfileId ?? 0,
  );

  const {
    checklist: meetingPrepChecklist,
    isLoading: meetingPrepLoading,
    isGenerating: meetingPrepGenerating,
    generateFromIep: generateMeetingPrep,
    reload: reloadMeetingPrep,
  } = useMeetingPrep(document?.childProfileId ?? 0, documentId);

  const loadSections = useCallback(async () => {
    const secRes = await getIepSections(documentId);
    if (secRes.success && secRes.data) {
      setSections(secRes.data);
    }
  }, [documentId]);

  useEffect(() => {
    async function load() {
      try {
        const docRes = await getIepDocument(documentId);
        if (docRes.success && docRes.data) {
          setDocument(docRes.data);

          if (docRes.data.status === "parsed") {
            await loadSections();
          }
        }
      } catch {
        // handled
      } finally {
        setIsLoading(false);
      }
    }
    load();
  }, [documentId, loadSections]);

  // Poll for document processing completion
  const pollDocumentStatus = useCallback(async () => {
    const res = await getIepDocument(documentId);
    if (res.success && res.data) {
      setDocument(res.data);
      if (res.data.status === "parsed") {
        await loadSections();
      }
    }
  }, [documentId, loadSections]);

  usePolling(pollDocumentStatus, 5000, document?.status === "processing");

  // Load other IEPs for comparison when document is available
  useEffect(() => {
    if (!document?.childProfileId) return;
    async function loadOthers() {
      try {
        const res = await getIepDocuments(document!.childProfileId);
        if (res.success && res.data) {
          setOtherIeps(res.data.filter((d) => d.id !== documentId));
        }
      } catch {
        // non-critical
      }
    }
    loadOthers();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [document?.childProfileId, documentId]);

  // Load child profile to know whether this IEP is "current"
  useEffect(() => {
    if (!document?.childProfileId) return;
    async function loadChild() {
      try {
        const res = await getChild(document!.childProfileId);
        if (res.success && res.data) {
          setChildCurrentIepId(res.data.currentIepDocumentId);
          setChildRole(res.data.role);
        }
      } catch {
        // non-critical
      }
    }
    loadChild();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [document?.childProfileId]);

  const handleMakeCurrent = async () => {
    if (!document) return;
    setSettingCurrent(true);
    try {
      const res = await setCurrentIep(document.childProfileId, document.id);
      if (res.success) setChildCurrentIepId(document.id);
    } catch {
      // handled by interceptor
    } finally {
      setSettingCurrent(false);
    }
  };

  // Close compare dropdown on outside click
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (
        compareRef.current &&
        !compareRef.current.contains(e.target as Node)
      ) {
        setCompareOpen(false);
      }
    }
    if (compareOpen) {
      window.addEventListener("mousedown", handleClickOutside);
      return () => window.removeEventListener("mousedown", handleClickOutside);
    }
  }, [compareOpen]);

  const handleDownload = async () => {
    const res = await getDownloadUrl(documentId);
    if (res.success && res.data) {
      window.open(res.data.url, "_blank");
    }
  };

  const handleReprocess = async () => {
    await reprocessIep(documentId);
    setDocument((prev) => (prev ? { ...prev, status: "processing" } : prev));
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (!document) {
    return (
      <div className="text-center py-12">
        <p className="text-brand-slate-400">Document not found.</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <div>
          <Link
            to={`/children/${document.childProfileId}`}
            className="inline-flex items-center gap-1.5 text-[13px] font-medium text-brand-slate-400 hover:text-brand-teal-500 transition-colors"
          >
            <ArrowLeft
              className="w-4 h-4"
              strokeWidth={1.8}
              aria-hidden="true"
            />
            Back to child
          </Link>
          <h1 className="font-serif text-[32px] font-semibold leading-tight mt-1 text-brand-slate-800">
            {document.fileName ||
              (document.meetingType
                ? MEETING_TYPE_LABELS[document.meetingType] ||
                  document.meetingType
                : `IEP #${document.id}`)}
          </h1>
          <div className="flex items-center gap-3 mt-2 flex-wrap">
            {document.meetingType && (
              <Badge variant="neutral">
                {MEETING_TYPE_LABELS[document.meetingType] ||
                  document.meetingType}
              </Badge>
            )}
            {childCurrentIepId === document.id && (
              <Badge variant="success" data-testid="current-iep-badge">
                Current IEP
              </Badge>
            )}
            {childCurrentIepId !== document.id && childRole && childRole !== "viewer" && (
              <button
                onClick={handleMakeCurrent}
                disabled={settingCurrent}
                data-testid="make-current-iep-button"
                className="text-[12px] font-medium text-brand-slate-400 hover:text-brand-teal-500 disabled:opacity-50 transition-colors"
              >
                {settingCurrent ? "Setting..." : "Make current"}
              </button>
            )}
            {document.iepDate && (
              <span className="text-[13px] text-brand-slate-500">
                {new Date(document.iepDate).toLocaleDateString()}
              </span>
            )}
            {document.attendees && (
              <span className="text-[13px] text-brand-slate-500">
                Attendees: {document.attendees}
              </span>
            )}
          </div>
          {document.notes && (
            <div className="mt-2">
              <button
                onClick={() => setNotesExpanded(!notesExpanded)}
                className="inline-flex items-center gap-1 text-[13px] font-medium text-brand-slate-500 hover:text-brand-teal-500 transition-colors"
              >
                {notesExpanded ? (
                  <ChevronUp
                    className="w-3.5 h-3.5"
                    strokeWidth={1.8}
                    aria-hidden="true"
                  />
                ) : (
                  <ChevronDown
                    className="w-3.5 h-3.5"
                    strokeWidth={1.8}
                    aria-hidden="true"
                  />
                )}
                Notes
              </button>
              {notesExpanded && (
                <p className="mt-1 text-sm text-brand-slate-600 whitespace-pre-wrap bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200">
                  {document.notes}
                </p>
              )}
            </div>
          )}
        </div>
        <div className="flex gap-2">
          {otherIeps.length > 0 && (
            <div className="relative" ref={compareRef}>
              <Button
                variant="secondary"
                onClick={() => setCompareOpen(!compareOpen)}
                data-testid="compare-button"
              >
                <ArrowRightLeft
                  className="w-4 h-4 mr-1.5"
                  strokeWidth={1.8}
                  aria-hidden="true"
                />
                Compare
              </Button>
              {compareOpen && (
                <div className="absolute right-0 top-full mt-1 w-64 bg-white rounded-card border border-brand-slate-200 shadow-lg z-20 py-1">
                  <p className="px-3 py-1.5 text-[11px] text-brand-slate-400 uppercase tracking-wide font-semibold">
                    Compare with...
                  </p>
                  {otherIeps.map((other) => (
                    <button
                      key={other.id}
                      className="w-full text-left px-3 py-2 text-sm text-brand-slate-700 hover:bg-brand-slate-50 transition-colors"
                      onClick={() => {
                        setCompareOpen(false);
                        navigate(
                          `/children/${document.childProfileId}/compare/${documentId}/${other.id}`,
                        );
                      }}
                    >
                      <span className="font-medium">
                        {other.iepDate
                          ? new Date(other.iepDate).toLocaleDateString()
                          : `IEP #${other.id}`}
                      </span>
                      {other.meetingType && (
                        <span className="text-brand-slate-400 ml-2 text-[12px]">
                          {MEETING_TYPE_LABELS[other.meetingType] ||
                            other.meetingType}
                        </span>
                      )}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
          <Button variant="ghost" onClick={handleDownload}>
            <Download
              className="w-4 h-4 mr-1.5"
              strokeWidth={1.8}
              aria-hidden="true"
            />
            Download PDF
          </Button>
          {(document.status === "error" || document.status === "uploaded") && (
            <Button onClick={handleReprocess}>
              <Play
                className="w-4 h-4 mr-1.5"
                strokeWidth={1.8}
                aria-hidden="true"
              />
              Process
            </Button>
          )}
        </div>
      </div>

      {document.status === "processing" && (
        <Notice variant="warning" title="Processing">
          Document is being processed. This may take a minute...
        </Notice>
      )}

      {document.status === "error" && (
        <Notice variant="error" title="Processing failed">
          Try re-uploading or click Process to retry.
        </Notice>
      )}

      {document.status === "uploaded" && (
        <Notice variant="info" title="Not yet processed">
          Document uploaded but not yet processed. Click Process to extract and
          analyze.
        </Notice>
      )}

      {document.status === "parsed" && sections.length > 0 && (
        <>
          {/* Tab bar */}
          <div className="flex border-b border-brand-slate-200">
            <button
              onClick={() => setActiveTab("document")}
              data-testid="tab-document"
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
              data-testid="tab-analysis"
              className={`px-4 py-2 text-[13px] font-medium transition-colors ${
                activeTab === "analysis"
                  ? "text-brand-slate-800 border-b-2 border-brand-teal-500"
                  : "text-brand-slate-400 hover:text-brand-slate-800"
              }`}
            >
              Analysis
              {analysis?.status === "completed" && (
                <span className="ml-2 inline-block w-2 h-2 rounded-full bg-brand-teal-500" />
              )}
            </button>
            <button
              onClick={() => setActiveTab("meeting-prep")}
              data-testid="tab-meeting-prep"
              className={`px-4 py-2 text-[13px] font-medium transition-colors ${
                activeTab === "meeting-prep"
                  ? "text-brand-slate-800 border-b-2 border-brand-teal-500"
                  : "text-brand-slate-400 hover:text-brand-slate-800"
              }`}
            >
              Meeting Prep
              {meetingPrepChecklist?.status === "completed" && (
                <span className="ml-2 inline-block w-2 h-2 rounded-full bg-brand-teal-500" />
              )}
            </button>
            <button
              onClick={() => setActiveTab("progress-reports")}
              data-testid="tab-progress-reports"
              className={`px-4 py-2 text-[13px] font-medium transition-colors ${
                activeTab === "progress-reports"
                  ? "text-brand-slate-800 border-b-2 border-brand-teal-500"
                  : "text-brand-slate-400 hover:text-brand-slate-800"
              }`}
            >
              Progress Reports
            </button>
          </div>

          {/* Tab content */}
          {activeTab === "document" && (
            <PdfViewer
              fileName={document.fileName}
              parsedNote={
                sections.length > 0
                  ? `We've already parsed this IEP. Head to the Analysis tab for findings, goal review, and advocacy alignment.`
                  : undefined
              }
              loadUrl={async () => {
                const res = await getDownloadUrl(documentId);
                return res.success && res.data ? res.data.url : null;
              }}
            />
          )}

          {activeTab === "analysis" && (
            <AnalysisTab
              analysis={analysis}
              isLoading={analysisLoading}
              isTriggering={isTriggering}
              advocacyGoals={advocacyGoals}
              onTrigger={triggerAnalysis}
              onReload={reloadAnalysis}
            />
          )}

          {activeTab === "meeting-prep" && (
            <MeetingPrepTab
              checklist={meetingPrepChecklist}
              isLoading={meetingPrepLoading}
              isGenerating={meetingPrepGenerating}
              onGenerate={() => generateMeetingPrep(documentId)}
              onReload={reloadMeetingPrep}
              analysisCreatedAt={
                analysis?.status === "completed" ? analysis.createdAt : null
              }
            />
          )}

          {activeTab === "progress-reports" && (
            <ProgressReportsTab
              iepId={documentId}
              childId={document.childProfileId}
              canEdit={childRole !== null && childRole !== "viewer"}
            />
          )}
        </>
      )}
    </div>
  );
}
