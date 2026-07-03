import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { Notice } from "@/components/ui/notice";
import { useToast } from "@/components/ui/toast";
import type { ChildOutletContext } from "@/features/children/components/child-detail-page";
import { useAnalysisRuns } from "../hooks/use-analysis-runs";
import { createRun } from "../api/analysis-runs-api";
import { SourcePicker } from "./source-picker";
import { RunHistoryList } from "./run-history-list";
import { RunDetail } from "./run-detail";
import type { CreateAnalysisRunRequest } from "../types";

function mapCreateError(status: number | undefined, message?: string): string {
  if (status === 402) return "Active subscription required";
  if (status === 403) return "You don't have permission";
  return message || "Could not start analysis";
}

export function ChildAnalysisTab() {
  const { child, childId } = useOutletContext<ChildOutletContext>();
  const { runs, isLoading, reload, hasInFlight, pollTimedOut } =
    useAnalysisRuns(childId);
  const [selectedRunId, setSelectedRunId] = useState<number | null>(null);
  const [isRunning, setIsRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [warning, setWarning] = useState<string | null>(null);
  const { show } = useToast();

  const isViewer = child.role === "viewer";

  const handleRun = async (payload: CreateAnalysisRunRequest) => {
    setIsRunning(true);
    setError(null);
    setWarning(null);
    try {
      const res = await createRun(childId, payload);
      if (res.success && res.data) {
        // A success message here is a non-blocking warning (e.g. duplicate sources).
        if (res.message) setWarning(res.message);
        setSelectedRunId(res.data.id);
        show({ message: "Analysis started", variant: "success" });
        await reload();
      } else {
        setError(res.message || "Could not start analysis");
      }
    } catch (err) {
      const axiosErr = err as { response?: { status?: number; data?: { message?: string } } };
      const status = axiosErr.response?.status;
      const apiMessage = axiosErr.response?.data?.message;
      setError(mapCreateError(status, apiMessage));
    } finally {
      setIsRunning(false);
    }
  };

  return (
    <div className="space-y-6">
      {error && (
        <Notice variant="error" title="Unable to run analysis">
          {error}
        </Notice>
      )}
      {warning && <Notice variant="warning" title="Heads up">{warning}</Notice>}
      {hasInFlight && pollTimedOut && (
        <Notice variant="info" title="Still working…">
          An analysis is taking longer than usual. It will appear here once it
          finishes.
        </Notice>
      )}

      <div className="grid gap-6 lg:grid-cols-[20rem_1fr]">
        <div className="space-y-6">
          {!isViewer && (
            <SourcePicker
              childId={childId}
              isRunning={isRunning}
              onRun={handleRun}
            />
          )}
          <RunHistoryList
            runs={runs}
            isLoading={isLoading}
            selectedRunId={selectedRunId}
            onSelect={setSelectedRunId}
          />
        </div>

        <div>
          {selectedRunId !== null ? (
            <RunDetail childId={childId} runId={selectedRunId} />
          ) : (
            <Notice variant="info" title="No analysis selected">
              Select a past analysis or run a new one to see results here.
            </Notice>
          )}
        </div>
      </div>
    </div>
  );
}
