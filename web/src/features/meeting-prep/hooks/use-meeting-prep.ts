import { useCallback, useEffect, useState } from "react";
import type { MeetingPrepChecklist } from "@/types/api";
import {
  getChecklistsByChild,
  generateFromGoals as apiGenerateFromGoals,
  generateFromIep as apiGenerateFromIep,
  generateFromEtr as apiGenerateFromEtr,
} from "../api/meeting-prep-api";
import { usePolling } from "@/hooks/use-polling";

export type MeetingPrepAnchor =
  | { type: "iep"; id: number }
  | { type: "etr"; id: number }
  | { type: "goals" };

function matchesAnchor(
  checklist: MeetingPrepChecklist,
  anchor: MeetingPrepAnchor | undefined,
  legacyIepId: number | undefined,
): boolean {
  if (anchor) {
    if (anchor.type === "iep") return checklist.iepDocumentId === anchor.id;
    if (anchor.type === "etr") return checklist.etrDocumentId === anchor.id;
    return true; // goals — show all (matches previous behavior)
  }
  if (legacyIepId) return checklist.iepDocumentId === legacyIepId;
  return true;
}

export function useMeetingPrep(
  childId: number,
  iepDocumentId?: number,
  anchor?: MeetingPrepAnchor,
) {
  const [checklist, setChecklist] = useState<MeetingPrepChecklist | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isGenerating, setIsGenerating] = useState(false);

  // Depend on primitives, not the anchor object reference (callers commonly
  // pass a fresh object literal each render, which would loop effects).
  const anchorType = anchor?.type;
  const anchorId = anchor && "id" in anchor ? anchor.id : undefined;

  const load = useCallback(async () => {
    if (!childId) {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    try {
      const response = await getChecklistsByChild(childId);
      if (response.success && response.data) {
        const resolvedAnchor: MeetingPrepAnchor | undefined =
          anchorType === "iep" && anchorId != null
            ? { type: "iep", id: anchorId }
            : anchorType === "etr" && anchorId != null
            ? { type: "etr", id: anchorId }
            : anchorType === "goals"
            ? { type: "goals" }
            : undefined;
        const filtered = response.data.filter((c) =>
          matchesAnchor(c, resolvedAnchor, iepDocumentId),
        );
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
  }, [childId, iepDocumentId, anchorType, anchorId]);

  useEffect(() => {
    load();
  }, [load]);

  const makePlaceholder = useCallback(
    (opts: { iepId?: number; etrId?: number }): MeetingPrepChecklist => ({
      id: 0,
      childProfileId: childId,
      iepDocumentId: opts.iepId ?? null,
      etrDocumentId: opts.etrId ?? null,
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
    }),
    [childId],
  );

  const generateFromGoals = useCallback(async () => {
    setIsGenerating(true);
    try {
      await apiGenerateFromGoals(childId);
      setChecklist((prev) =>
        prev ? { ...prev, status: "generating" } : makePlaceholder({}),
      );
    } catch {
      // handled by caller
    } finally {
      setIsGenerating(false);
    }
  }, [childId, makePlaceholder]);

  const generateFromIep = useCallback(
    async (iepId: number) => {
      setIsGenerating(true);
      try {
        await apiGenerateFromIep(iepId);
        setChecklist((prev) =>
          prev
            ? { ...prev, status: "generating" }
            : makePlaceholder({ iepId }),
        );
      } catch {
        // handled by caller
      } finally {
        setIsGenerating(false);
      }
    },
    [makePlaceholder],
  );

  const generateFromEtr = useCallback(
    async (etrId: number) => {
      setIsGenerating(true);
      try {
        await apiGenerateFromEtr(etrId);
        setChecklist((prev) =>
          prev
            ? { ...prev, status: "generating" }
            : makePlaceholder({ etrId }),
        );
      } catch {
        // handled by caller
      } finally {
        setIsGenerating(false);
      }
    },
    [makePlaceholder],
  );

  // Poll while checklist is generating
  const pollStatus = useCallback(async () => {
    if (!childId) return;
    const response = await getChecklistsByChild(childId);
    if (response.success && response.data) {
      const resolvedAnchor: MeetingPrepAnchor | undefined =
        anchorType === "iep" && anchorId != null
          ? { type: "iep", id: anchorId }
          : anchorType === "etr" && anchorId != null
          ? { type: "etr", id: anchorId }
          : anchorType === "goals"
          ? { type: "goals" }
          : undefined;
      const filtered = response.data.filter((c) =>
        matchesAnchor(c, resolvedAnchor, iepDocumentId),
      );
      const sorted = filtered.sort(
        (a, b) =>
          new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
      );
      if (sorted[0]) {
        setChecklist(sorted[0]);
      }
    }
  }, [childId, iepDocumentId, anchorType, anchorId]);

  const isInProgress =
    checklist?.status === "generating" || checklist?.status === "pending";
  usePolling(pollStatus, 5000, isInProgress);

  return {
    checklist,
    isLoading,
    isGenerating,
    generateFromGoals,
    generateFromIep,
    generateFromEtr,
    reload: load,
  };
}
