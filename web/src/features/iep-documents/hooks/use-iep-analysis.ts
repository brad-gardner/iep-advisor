import type { GoalAnalysis } from "@/types/api";
import { useSourceAnalysis } from "@/features/analysis/hooks/use-source-analysis";
import type {
  AnalysisRunLatest,
  AnalysisRunOtherSource,
  AnalysisRunSection,
  AnalysisRunSource,
} from "@/features/analysis/types";

export interface UseIepAnalysisResult {
  /** The latest run including this document, or null when it has never been
   * analyzed (also null before the first load finishes, or while a non-404
   * load error is showing — see `loadError`). */
  run: AnalysisRunLatest | null;
  /** This document's own entry within `run.sources`. */
  source: AnalysisRunSource | null;
  /** This document's own sections (matched by `analysisRunSourceId`), ordinary
   * sections only — `iep_goals` is surfaced separately as `goalAnalyses`. */
  sections: AnalysisRunSection[];
  /** This document's goal ratings, from its `iep_goals` section. */
  goalAnalyses: GoalAnalysis[] | null;
  /** The run's other sources, when this document was analyzed alongside others. */
  otherSources: AnalysisRunOtherSource[];
  /** Whether the document was re-processed since this run, per the server. */
  stale: boolean;
  isLoading: boolean;
  /** Set when loading the latest run fails for a reason other than "never
   * analyzed". The previous `run` is kept in state; offer "Try again" via
   * `reload`. */
  loadError: string | null;
  isTriggering: boolean;
  /** Set when `trigger()` fails (limit reached, no subscription, permission). */
  triggerError: string | null;
  /** Starts (or restarts) a single-source run for this document and polls
   * until it reaches a terminal status. */
  trigger: () => Promise<void>;
  reload: () => Promise<void>;
}

export function useIepAnalysis(childId: number, documentId: number): UseIepAnalysisResult {
  const {
    run,
    source,
    sections,
    sectionOfKind,
    otherSources,
    stale,
    isLoading,
    loadError,
    isTriggering,
    triggerError,
    trigger,
    reload,
  } = useSourceAnalysis(childId, "IepDocument", documentId);

  const goalAnalyses = sectionOfKind("iep_goals")?.goalAnalyses ?? null;

  return {
    run,
    source,
    sections,
    goalAnalyses,
    otherSources,
    stale,
    isLoading,
    loadError,
    isTriggering,
    triggerError,
    trigger,
    reload,
  };
}
