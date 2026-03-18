import { useCallback, useEffect, useState } from "react";
import type { MeetingPrepChecklist } from "@/types/api";
import {
  getChecklistsByChild,
  generateFromGoals as apiGenerateFromGoals,
  generateFromIep as apiGenerateFromIep,
} from "../api/meeting-prep-api";
import { usePolling } from "@/hooks/use-polling";

export function useMeetingPrep(childId: number, iepDocumentId?: number) {
  const [checklist, setChecklist] = useState<MeetingPrepChecklist | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isGenerating, setIsGenerating] = useState(false);

  const load = useCallback(async () => {
    if (!childId) {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    try {
      const response = await getChecklistsByChild(childId);
      if (response.success && response.data) {
        const filtered = iepDocumentId
          ? response.data.filter((c) => c.iepDocumentId === iepDocumentId)
          : response.data;
        // Most recent first
        const sorted = filtered.sort(
          (a, b) =>
            new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
        );
        setChecklist(sorted[0] ?? null);
      } else {
        setChecklist(null);
      }
    } catch {
      setChecklist(null);
    } finally {
      setIsLoading(false);
    }
  }, [childId, iepDocumentId]);

  useEffect(() => {
    load();
  }, [load]);

  const generateFromGoals = useCallback(async () => {
    setIsGenerating(true);
    try {
      await apiGenerateFromGoals(childId);
      setChecklist((prev) =>
        prev
          ? { ...prev, status: "generating" }
          : {
              id: 0,
              childProfileId: childId,
              iepDocumentId: null,
              status: "generating",
              questionsToAsk: [],
              documentsToBring: [],
              redFlagsToRaise: [],
              rightsToReference: [],
              goalGaps: [],
              generalTips: [],
              preparationNotes: [],
              errorMessage: null,
              createdAt: new Date().toISOString(),
            },
      );
    } catch {
      // handled by caller
    } finally {
      setIsGenerating(false);
    }
  }, [childId]);

  const generateFromIep = useCallback(
    async (iepId: number) => {
      setIsGenerating(true);
      try {
        await apiGenerateFromIep(iepId);
        setChecklist((prev) =>
          prev
            ? { ...prev, status: "generating" }
            : {
                id: 0,
                childProfileId: childId,
                iepDocumentId: iepId,
                status: "generating",
                questionsToAsk: [],
                documentsToBring: [],
                redFlagsToRaise: [],
                rightsToReference: [],
                goalGaps: [],
                generalTips: [],
                preparationNotes: [],
                errorMessage: null,
                createdAt: new Date().toISOString(),
              },
        );
      } catch {
        // handled by caller
      } finally {
        setIsGenerating(false);
      }
    },
    [childId],
  );

  // Poll while checklist is generating
  const pollStatus = useCallback(async () => {
    if (!childId) return;
    const response = await getChecklistsByChild(childId);
    if (response.success && response.data) {
      const filtered = iepDocumentId
        ? response.data.filter((c) => c.iepDocumentId === iepDocumentId)
        : response.data;
      const sorted = filtered.sort(
        (a, b) =>
          new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
      );
      if (sorted[0]) {
        setChecklist(sorted[0]);
      }
    }
  }, [childId, iepDocumentId]);

  const isInProgress =
    checklist?.status === "generating" || checklist?.status === "pending";
  usePolling(pollStatus, 5000, isInProgress);

  return {
    checklist,
    isLoading,
    isGenerating,
    generateFromGoals,
    generateFromIep,
    reload: load,
  };
}
