import { useCallback, useEffect, useState } from "react";
import type { IepAnalysis } from "@/types/api";
import { getAnalysis, triggerAnalysis } from "../api/iep-documents-api";
import { usePolling, ANALYSIS_MAX_POLLS } from "@/hooks/use-polling";

export function useIepAnalysis(documentId: number) {
  const [analysis, setAnalysis] = useState<IepAnalysis | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isTriggering, setIsTriggering] = useState(false);

  const load = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await getAnalysis(documentId);
      if (response.success && response.data) {
        setAnalysis(response.data);
      } else {
        setAnalysis(null);
      }
    } catch {
      setAnalysis(null);
    } finally {
      setIsLoading(false);
    }
  }, [documentId]);

  useEffect(() => {
    load();
  }, [load]);

  const trigger = useCallback(async () => {
    setIsTriggering(true);
    try {
      await triggerAnalysis(documentId);
      setAnalysis((prev) =>
        prev
          ? { ...prev, status: "analyzing" }
          : {
              id: 0,
              iepDocumentId: documentId,
              status: "analyzing",
              overallSummary: null,
              sectionAnalyses: [],
              goalAnalyses: [],
              overallRedFlags: [],
              advocacyGapAnalysis: null,
              parentGoalsSnapshot: null,
              errorMessage: null,
              createdAt: new Date().toISOString(),
            },
      );
    } catch {
      // handled by caller
    } finally {
      setIsTriggering(false);
    }
  }, [documentId]);

  // Poll while analysis is in progress
  const pollStatus = useCallback(async () => {
    const response = await getAnalysis(documentId);
    if (response.success && response.data) {
      setAnalysis(response.data);
    }
  }, [documentId]);

  const isInProgress =
    analysis?.status === "analyzing" || analysis?.status === "pending";
  // A full analysis runs several minutes; the default 5-minute cap froze the page on
  // "Analyzing" while the server was still working. Match the server's 15-minute Claude timeout.
  usePolling(pollStatus, 5000, isInProgress, ANALYSIS_MAX_POLLS);

  return { analysis, isLoading, isTriggering, trigger, reload: load };
}
