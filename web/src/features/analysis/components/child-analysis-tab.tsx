import { useEffect, useState } from "react";
import { useOutletContext, useSearchParams } from "react-router-dom";
import type { SetURLSearchParams } from "react-router-dom";
import { Notice } from "@/components/ui/notice";
import { useToast } from "@/components/ui/toast";
import type { ChildOutletContext } from "@/features/children/components/child-detail-page";
import { useAnalysisRuns } from "../hooks/use-analysis-runs";
import { createRun } from "../api/analysis-runs-api";
import { SourcePicker } from "./source-picker";
import { RunHistoryList } from "./run-history-list";
import { RunDetail } from "./run-detail";
import type { CreateAnalysisRunRequest } from "../types";

const RUN_PARAM = "run";

function mapCreateError(status: number | undefined, message?: string): string {
  if (status === 402) return "Active subscription required";
  if (status === 403) return "You don't have permission";
  return message || "Could not start analysis";
}

/** A run id from the URL: digits only, else not a usable selection. */
function parseRunId(value: string | null): number | null {
  if (value === null || !/^\d+$/.test(value)) return null;
  return Number(value);
}

function setRunParam(
  setSearchParams: SetURLSearchParams,
  runId: number,
  options?: { replace?: boolean }
) {
  setSearchParams((prev) => {
    const next = new URLSearchParams(prev);
    next.set(RUN_PARAM, String(runId));
    return next;
  }, options);
}

export function ChildAnalysisTab() {
  const { child, childId } = useOutletContext<ChildOutletContext>();
  const { runs, isLoading, reload, hasInFlight, pollTimedOut } =
    useAnalysisRuns(childId);
  const [searchParams, setSearchParams] = useSearchParams();
  const [isRunning, setIsRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [warning, setWarning] = useState<string | null>(null);
  const { show } = useToast();

  const isViewer = child.role === "viewer";

  const requestedRunId = parseRunId(searchParams.get(RUN_PARAM));
  const latestRunId = runs.length > 0 ? runs[0].id : null;
  const requestedRunExists =
    requestedRunId !== null && runs.some((run) => run.id === requestedRunId);
  const selectedRunId = requestedRunExists ? requestedRunId : latestRunId;

  // Whether the run id in the URL is a dangling one (present but matching no
  // run this child has), evaluated once per distinct requestedRunId. `correctedTo`
  // remembers the id we redirected a bad request to, so that the correction
  // itself (the id now "exists" once the URL is fixed) isn't mistaken for a
  // fresh, direct navigation that should clear the notice.
  //
  // This adjusts state during rendering rather than in an effect — see
  // https://react.dev/learn/you-might-not-need-an-effect#adjusting-some-state-when-a-prop-changes
  // — because the notice is derived from a URL value that disappears once
  // corrected, so it can't be computed from the current render's inputs alone.
  const [notice, setNotice] = useState<{
    checkedRunId: number | null;
    show: boolean;
    correctedTo: number | null;
  }>({ checkedRunId: null, show: false, correctedTo: null });
  if (!isLoading && notice.checkedRunId !== requestedRunId) {
    if (requestedRunId !== null && !requestedRunExists) {
      setNotice({ checkedRunId: requestedRunId, show: true, correctedTo: latestRunId });
    } else if (
      requestedRunId !== null &&
      requestedRunExists &&
      notice.correctedTo === requestedRunId
    ) {
      setNotice({ checkedRunId: requestedRunId, show: true, correctedTo: null });
    } else {
      setNotice({ checkedRunId: requestedRunId, show: false, correctedTo: null });
    }
  }
  const runNotFound = notice.show;

  // The one actual side effect: push the URL to a good run id when it's
  // missing (auto-select the latest, replacing so it doesn't add a history
  // entry) or doesn't match any run this child has (fall back to the
  // latest — the notice above already covers telling the parent why).
  useEffect(() => {
    if (isLoading) return;
    if (requestedRunId !== null && requestedRunExists) return;
    if (latestRunId !== null) {
      setRunParam(setSearchParams, latestRunId, { replace: true });
    }
  }, [isLoading, requestedRunId, requestedRunExists, latestRunId, setSearchParams]);

  const handleSelect = (runId: number) => {
    setNotice({ checkedRunId: runId, show: false, correctedTo: null });
    setRunParam(setSearchParams, runId);
  };

  const handleRun = async (payload: CreateAnalysisRunRequest) => {
    setIsRunning(true);
    setError(null);
    setWarning(null);
    try {
      const res = await createRun(childId, payload);
      if (res.success && res.data) {
        // A success message here is a non-blocking warning (e.g. duplicate sources).
        if (res.message) setWarning(res.message);
        setNotice({ checkedRunId: res.data.id, show: false, correctedTo: null });
        setRunParam(setSearchParams, res.data.id);
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
      {runNotFound && (
        <Notice
          variant="info"
          title="That analysis couldn't be found — showing the latest."
          data-testid="analysis-run-not-found"
        />
      )}
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
            onSelect={handleSelect}
          />
        </div>

        <div>
          {selectedRunId !== null ? (
            <RunDetail childId={childId} runId={selectedRunId} canAsk={!isViewer} />
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
