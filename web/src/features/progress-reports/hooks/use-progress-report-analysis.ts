import { useCallback, useEffect, useRef, useState } from "react";
import axios from "axios";
import { usePolling } from "@/hooks/use-polling";
import { useToast } from "@/components/ui/toast";
import { getAnalysis, startAnalysis as startAnalysisApi } from "../api/progress-reports-api";
import type { ProgressReportAnalysis } from "../types";

export type UseProgressReportAnalysisStatus = "none" | ProgressReportAnalysis["status"];

export interface UseProgressReportAnalysisResult {
  analysis: ProgressReportAnalysis | null;
  status: UseProgressReportAnalysisStatus;
  loading: boolean;
  isTriggering: boolean;
  error: string | null;
  start: () => Promise<void>;
  refresh: () => Promise<void>;
}

export function useProgressReportAnalysis(
  progressReportId: number
): UseProgressReportAnalysisResult {
  const [analysis, setAnalysis] = useState<ProgressReportAnalysis | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [isTriggering, setIsTriggering] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const { show } = useToast();
  // Tracks the last-seen status so the poll can fire a single success toast
  // when an in-progress analysis transitions to completed.
  const prevStatusRef = useRef<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await getAnalysis(progressReportId);
      if (response.success && response.data) {
        setAnalysis(response.data);
        prevStatusRef.current = response.data.status;
      } else {
        setAnalysis(null);
        prevStatusRef.current = null;
      }
    } catch (err: unknown) {
      if (axios.isAxiosError(err) && err.response?.status === 404) {
        setAnalysis(null);
        prevStatusRef.current = null;
      } else {
        setError(
          axios.isAxiosError(err)
            ? err.response?.data?.message || err.message
            : "Failed to load analysis"
        );
        setAnalysis(null);
      }
    } finally {
      setLoading(false);
    }
  }, [progressReportId]);

  useEffect(() => {
    void load();
  }, [load]);

  const start = useCallback(async () => {
    setIsTriggering(true);
    setError(null);
    try {
      const response = await startAnalysisApi(progressReportId);
      if (!response.success) {
        setError(response.message || "Failed to start analysis");
        return;
      }
      prevStatusRef.current = "analyzing";
      setAnalysis((prev) =>
        prev
          ? { ...prev, status: "analyzing", errorMessage: null }
          : {
              id: 0,
              progressReportId,
              status: "analyzing",
              summary: null,
              goalProgressFindings: [],
              redFlags: [],
              advocacyGapAnalysis: null,
              parentGoalsSnapshot: null,
              iepGoalsSnapshot: [],
              errorMessage: null,
              createdAt: new Date().toISOString(),
            }
      );
    } catch (err: unknown) {
      setError(
        axios.isAxiosError(err)
          ? err.response?.data?.message || err.message
          : "Failed to start analysis"
      );
    } finally {
      setIsTriggering(false);
    }
  }, [progressReportId]);

  const pollStatus = useCallback(async () => {
    try {
      const response = await getAnalysis(progressReportId);
      if (response.success && response.data) {
        const next = response.data;
        const prevStatus = prevStatusRef.current;
        setAnalysis(next);
        if (
          (prevStatus === "analyzing" || prevStatus === "pending") &&
          next.status === "completed"
        ) {
          show({ message: "Analysis ready", variant: "success" });
        }
        prevStatusRef.current = next.status;
      }
    } catch {
      // swallow during polling
    }
  }, [progressReportId, show]);

  const isInProgress =
    analysis?.status === "analyzing" || analysis?.status === "pending";
  usePolling(pollStatus, 3000, isInProgress);

  const status: UseProgressReportAnalysisStatus = analysis ? analysis.status : "none";

  return {
    analysis,
    status,
    loading,
    isTriggering,
    error,
    start,
    refresh: load,
  };
}
