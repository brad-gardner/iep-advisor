import { useSourceAnalysis } from "@/features/analysis/hooks/use-source-analysis";
import type {
  AnalysisRunLatest,
  AnalysisRunOtherSource,
  AnalysisRunSection,
  AnalysisRunSource,
  EtrCompletenessPayload,
  EtrEligibilityPayload,
} from "@/features/analysis/types";

function asArray<T>(value: T[] | null | undefined): T[] {
  return Array.isArray(value) ? value : [];
}

// Defensive normalization: the server should always send arrays for these
// list fields, but a deserialization gap (or a future server change) could
// coalesce one to null instead of []. Normalize once here so the ETR views
// downstream can call `.join`/`.length`/`.map` on them without guarding.
function normalizeCompleteness(
  payload: EtrCompletenessPayload | null | undefined
): EtrCompletenessPayload | null {
  if (!payload) return null;
  return {
    ...payload,
    evaluatedDomains: asArray(payload.evaluatedDomains).map((d) => ({
      ...d,
      toolsUsed: asArray(d.toolsUsed),
    })),
    missingDomains: asArray(payload.missingDomains),
  };
}

function normalizeEligibility(
  payload: EtrEligibilityPayload | null | undefined
): EtrEligibilityPayload | null {
  if (!payload) return null;
  return {
    ...payload,
    supportingEvidence: asArray(payload.supportingEvidence),
    contradictingEvidence: asArray(payload.contradictingEvidence),
    alternativeConsiderations: asArray(payload.alternativeConsiderations),
  };
}

export interface UseEtrAnalysisResult {
  /** The latest run including this ETR, or null when it has never been
   * analyzed (also null before the first load finishes, or while a non-404
   * load error is showing — see `loadError`). */
  run: AnalysisRunLatest | null;
  /** This ETR's own entry within `run.sources`. */
  source: AnalysisRunSource | null;
  /** This ETR's own sections (matched by `analysisRunSourceId`), ordinary
   * sections only — `etr_completeness`/`etr_eligibility` are surfaced
   * separately as `completeness`/`eligibility`. */
  sections: AnalysisRunSection[];
  /** This ETR's assessment-completeness rating, from its `etr_completeness`
   * section, with every list field normalized to `[]` when absent. */
  completeness: EtrCompletenessPayload | null;
  /** This ETR's eligibility review, from its `etr_eligibility` section, with
   * every list field normalized to `[]` when absent. */
  eligibility: EtrEligibilityPayload | null;
  /** The run's other sources, when this ETR was analyzed alongside others. */
  otherSources: AnalysisRunOtherSource[];
  /** Whether the ETR was re-processed since this run, per the server. */
  stale: boolean;
  isLoading: boolean;
  /** Set when loading the latest run fails for a reason other than "never
   * analyzed". The previous `run` is kept in state; offer "Try again" via
   * `reload`. */
  loadError: string | null;
  isTriggering: boolean;
  /** Set when `trigger()` fails (limit reached, no subscription, permission). */
  triggerError: string | null;
  /** Starts (or restarts) a single-source run for this ETR and polls until it
   * reaches a terminal status. */
  trigger: () => Promise<void>;
  reload: () => Promise<void>;
}

export function useEtrAnalysis(childId: number, etrId: number): UseEtrAnalysisResult {
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
  } = useSourceAnalysis(childId, "EtrDocument", etrId);

  const completeness = normalizeCompleteness(sectionOfKind("etr_completeness")?.etrCompleteness);
  const eligibility = normalizeEligibility(sectionOfKind("etr_eligibility")?.etrEligibility);

  return {
    run,
    source,
    sections,
    completeness,
    eligibility,
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
