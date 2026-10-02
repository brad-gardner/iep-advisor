import { useCallback, useEffect, useRef, useState } from "react";
import { usePolling, ANALYSIS_MAX_POLLS } from "@/hooks/use-polling";
import { createRun, getLatestForSource } from "../api/analysis-runs-api";
import { mapCreateError } from "../lib/map-create-error";
import {
  isTerminalStatus,
  type AnalysisRunLatest,
  type AnalysisRunOtherSource,
  type AnalysisRunSection,
  type AnalysisRunSource,
  type AnalysisSourceType,
} from "../types";

const POLL_INTERVAL_MS = 5000;

export interface UseSourceAnalysisResult {
  /** The latest run including this source, or null when it has never been
   * analyzed (also null before the first load finishes, or while a non-404
   * load error is showing — see `loadError`). */
  run: AnalysisRunLatest | null;
  /** This source's own entry within `run.sources`. */
  source: AnalysisRunSource | null;
  /** This source's own ordinary sections (matched by `analysisRunSourceId`,
   * `analysis` populated) — the per-kind payload sections (`iep_goals`,
   * `etr_completeness`, `etr_eligibility`) are reached via `sectionOfKind`. */
  sections: AnalysisRunSection[];
  /** This source's own section of a given `sectionKind`, if any — used to
   * reach a typed payload (`goalAnalyses`, `etrCompleteness`, `etrEligibility`). */
  sectionOfKind: (kind: string) => AnalysisRunSection | undefined;
  /** The run's other sources, when this source was analyzed alongside others. */
  otherSources: AnalysisRunOtherSource[];
  /** Whether the source was re-processed since this run, per the server. */
  stale: boolean;
  isLoading: boolean;
  /** Set when loading the latest run fails for a reason other than "never
   * analyzed" (a 404). The previous `run` is kept in state so the page can
   * keep showing it; callers should offer a "Try again" action via `reload`. */
  loadError: string | null;
  isTriggering: boolean;
  /** Set when `trigger()` fails (limit reached, no subscription, permission). */
  triggerError: string | null;
  /** Starts (or restarts) a single-source run for this source and polls
   * until it reaches a terminal status. */
  trigger: () => Promise<void>;
  reload: () => Promise<void>;
}

/**
 * Loads and polls the latest analysis run that includes a single document
 * (IEP, ETR, or any other analyzable source), and starts new runs for it.
 * `useIepAnalysis`/`useEtrAnalysis` are thin, document-type-specific wrappers
 * over this hook.
 */
export function useSourceAnalysis(
  childId: number,
  sourceType: AnalysisSourceType,
  sourceId: number
): UseSourceAnalysisResult {
  const [run, setRun] = useState<AnalysisRunLatest | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isTriggering, setIsTriggering] = useState(false);
  const [triggerError, setTriggerError] = useState<string | null>(null);

  // Bumped by load(), trigger(), and a childId/sourceType/sourceId change,
  // and read back once a load/poll request settles — a response whose
  // generation has since gone stale (superseded by a newer load, a trigger
  // seed, or a source change) is dropped instead of clobbering fresher
  // state. Same pattern as use-analysis-runs.ts.
  const generationRef = useRef(0);

  // A new source resets state immediately rather than showing the previous
  // source's run (or misjudging staleness against it) while the new
  // source's request is in flight.
  useEffect(() => {
    generationRef.current += 1;
    setRun(null);
    setLoadError(null);
    setIsLoading(true);
  }, [childId, sourceType, sourceId]);

  const applyLatestResult = useCallback(
    (res: Awaited<ReturnType<typeof getLatestForSource>>) => {
      if (res.success && res.data) {
        setRun(res.data);
        setLoadError(null);
      }
    },
    []
  );

  const handleLoadError = useCallback((err: unknown) => {
    const axiosErr = err as { response?: { status?: number; data?: { message?: string } } };
    if (axiosErr.response?.status === 404) {
      // No analysis has ever included this source — a true "never analyzed",
      // not an error to recover from.
      setRun(null);
      setLoadError(null);
      return;
    }
    // Any other failure (network blip, 5xx, etc.): keep showing the last
    // known run instead of dropping back to an empty/"never analyzed" state,
    // and surface a retry affordance instead.
    setLoadError(axiosErr.response?.data?.message || "Could not load this analysis.");
  }, []);

  const load = useCallback(async () => {
    if (!childId || !sourceId) {
      // Not a real id yet (e.g. the page's own document/childId hasn't
      // loaded) — stay in the loading state rather than flipping to false
      // and flashing a "never analyzed" empty state before the first real
      // fetch (once childId/sourceId become valid) gets to run.
      return;
    }
    const generation = ++generationRef.current;
    setIsLoading(true);
    try {
      const res = await getLatestForSource(childId, sourceType, sourceId);
      if (generation !== generationRef.current) return;
      applyLatestResult(res);
    } catch (err) {
      if (generation !== generationRef.current) return;
      handleLoadError(err);
    } finally {
      if (generation === generationRef.current) setIsLoading(false);
    }
  }, [childId, sourceType, sourceId, applyLatestResult, handleLoadError]);

  useEffect(() => {
    load();
  }, [load]);

  const refreshInBackground = useCallback(async () => {
    if (!childId || !sourceId) return;
    const generation = generationRef.current;
    try {
      const res = await getLatestForSource(childId, sourceType, sourceId);
      if (generation !== generationRef.current) return;
      applyLatestResult(res);
    } catch (err) {
      if (generation !== generationRef.current) return;
      handleLoadError(err);
    }
  }, [childId, sourceType, sourceId, applyLatestResult, handleLoadError]);

  const source =
    run?.sources.find((s) => s.sourceType === sourceType && s.sourceId === sourceId) ?? null;

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
        sources: [{ sourceType, sourceId }],
      });
      if (res.success && res.data) {
        // Bump the generation before seeding so that any older in-flight
        // load/poll request (e.g. a reload kicked off just before this
        // trigger) is dropped on arrival instead of overwriting the run
        // we're about to seed.
        generationRef.current += 1;
        // Seed in-flight state from the create response immediately: a
        // failed follow-up load (a non-404 error) must not drop this back to
        // null/"never analyzed", and polling should start right away rather
        // than waiting on `load()` to land.
        const created = res.data;
        setRun({ ...created, otherSources: [], stale: false });
        setLoadError(null);
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
  }, [childId, sourceType, sourceId, load]);

  const sections = source
    ? run!.sections.filter((s) => s.analysisRunSourceId === source.id && s.analysis !== null)
    : [];

  const sectionOfKind = useCallback(
    (kind: string) =>
      source
        ? run?.sections.find((s) => s.analysisRunSourceId === source.id && s.sectionKind === kind)
        : undefined,
    [run, source]
  );

  return {
    run,
    source,
    sections,
    sectionOfKind,
    otherSources: run?.otherSources ?? [],
    stale: run?.stale ?? false,
    isLoading,
    loadError,
    isTriggering,
    triggerError,
    trigger,
    reload: load,
  };
}
