import { useCallback, useEffect, useState } from "react";
import { usePolling, ANALYSIS_MAX_POLLS } from "@/hooks/use-polling";
import { createRun, getLatestForSource } from "@/features/analysis/api/analysis-runs-api";
import {
  isTerminalStatus,
  type AnalysisRunLatest,
  type AnalysisRunOtherSource,
  type AnalysisRunSection,
  type AnalysisRunSource,
  type EtrCompletenessPayload,
  type EtrEligibilityPayload,
} from "@/features/analysis/types";

const POLL_INTERVAL_MS = 5000;

function mapCreateError(status: number | undefined, message?: string): string {
  if (status === 402) return "Active subscription required";
  if (status === 403) return "You don't have permission";
  return message || "Could not start analysis";
}

export interface UseEtrAnalysisResult {
  /** The latest run including this ETR, or null when it has never been
   * analyzed (also null before the first load finishes). */
  run: AnalysisRunLatest | null;
  /** This ETR's own entry within `run.sources`. */
  source: AnalysisRunSource | null;
  /** This ETR's own sections (matched by `analysisRunSourceId`), ordinary
   * sections only — `etr_completeness`/`etr_eligibility` are surfaced
   * separately as `completeness`/`eligibility`. */
  sections: AnalysisRunSection[];
  /** This ETR's assessment-completeness rating, from its `etr_completeness` section. */
  completeness: EtrCompletenessPayload | null;
  /** This ETR's eligibility review, from its `etr_eligibility` section. */
  eligibility: EtrEligibilityPayload | null;
  /** The run's other sources, when this ETR was analyzed alongside others. */
  otherSources: AnalysisRunOtherSource[];
  /** Whether the ETR was re-processed since this run, per the server. */
  stale: boolean;
  isLoading: boolean;
  isTriggering: boolean;
  /** Set when `trigger()` fails (limit reached, no subscription, permission). */
  triggerError: string | null;
  /** Starts (or restarts) a single-source run for this ETR and polls until it
   * reaches a terminal status. */
  trigger: () => Promise<void>;
  reload: () => Promise<void>;
}

export function useEtrAnalysis(childId: number, etrId: number): UseEtrAnalysisResult {
  const [run, setRun] = useState<AnalysisRunLatest | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isTriggering, setIsTriggering] = useState(false);
  const [triggerError, setTriggerError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!childId || !etrId) {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    try {
      const res = await getLatestForSource(childId, "EtrDocument", etrId);
      setRun(res.success && res.data ? res.data : null);
    } catch {
      // A 404 ("no analysis found for this ETR") means never analyzed; any
      // other failure is handled by the api-client interceptor (401 redirect,
      // 5xx -> Sentry). Either way there is no run to show.
      setRun(null);
    } finally {
      setIsLoading(false);
    }
  }, [childId, etrId]);

  useEffect(() => {
    load();
  }, [load]);

  const refreshInBackground = useCallback(async () => {
    if (!childId || !etrId) return;
    try {
      const res = await getLatestForSource(childId, "EtrDocument", etrId);
      if (res.success && res.data) setRun(res.data);
    } catch {
      // ignore transient polling errors
    }
  }, [childId, etrId]);

  const source =
    run?.sources.find((s) => s.sourceType === "EtrDocument" && s.sourceId === etrId) ?? null;

  const inFlight =
    run !== null &&
    (!isTerminalStatus(run.status) ||
      source?.status === "Pending" ||
      source?.status === "Running");

  usePolling(refreshInBackground, POLL_INTERVAL_MS, inFlight, ANALYSIS_MAX_POLLS);

  const trigger = useCallback(async () => {
    setIsTriggering(true);
    setTriggerError(null);
    try {
      const res = await createRun(childId, {
        sources: [{ sourceType: "EtrDocument", sourceId: etrId }],
      });
      if (res.success && res.data) {
        await load();
      } else {
        setTriggerError(res.message || "Could not start analysis");
      }
    } catch (err) {
      const axiosErr = err as { response?: { status?: number; data?: { message?: string } } };
      setTriggerError(
        mapCreateError(axiosErr.response?.status, axiosErr.response?.data?.message)
      );
    } finally {
      setIsTriggering(false);
    }
  }, [childId, etrId, load]);

  const sections = source
    ? run!.sections.filter((s) => s.analysisRunSourceId === source.id && s.analysis !== null)
    : [];
  const completeness =
    (source
      ? run!.sections.find(
          (s) => s.analysisRunSourceId === source.id && s.sectionKind === "etr_completeness"
        )?.etrCompleteness
      : null) ?? null;
  const eligibility =
    (source
      ? run!.sections.find(
          (s) => s.analysisRunSourceId === source.id && s.sectionKind === "etr_eligibility"
        )?.etrEligibility
      : null) ?? null;

  return {
    run,
    source,
    sections,
    completeness,
    eligibility,
    otherSources: run?.otherSources ?? [],
    stale: run?.stale ?? false,
    isLoading,
    isTriggering,
    triggerError,
    trigger,
    reload: load,
  };
}
