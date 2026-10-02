import type {
  AdvocacyGapAnalysis,
  GoalAnalysis,
  LegalReference,
  ParentGoalSnapshot,
  RedFlag,
} from "@/types/api";

export type AnalysisRunStatus = "Pending" | "Running" | "Completed" | "Error";

export type AnalysisSourceType =
  | "IepDocument"
  | "EtrDocument"
  | "ProgressReport";

// The deserialized per-source section payload (AnalysisRunSectionResult on the
// backend). The controller serializes this typed object, so it arrives as an
// object rather than a JSON string.
export interface AnalysisRunSectionAnalysis {
  sectionKind: string;
  plainLanguageSummary: string;
  keyPoints: string[];
  redFlags: RedFlag[];
  legalReferences: LegalReference[];
}

export interface AnalysisRunSource {
  id: number;
  sourceType: string;
  sourceId: number;
  sourceLabel: string | null;
  /** Shares its value set with AnalysisRunStatus (Pending|Running|Completed|Error). */
  status: AnalysisRunStatus;
  /** User-safe failure text; set only when status is "Error". */
  errorMessage: string | null;
}

export interface AnalysisRunSection {
  id: number;
  analysisRunSourceId: number | null;
  sectionKind: string;
  analysis: AnalysisRunSectionAnalysis | null;
  /** Populated only when sectionKind is "iep_goals"; null otherwise, including
   * when that section's JSON failed to deserialize. */
  goalAnalyses: GoalAnalysis[] | null;
  displayOrder: number;
}

export interface CrossDocSynthesis {
  summary: string;
  timeline: string[];
  contradictions: string[];
  progression: string | null;
}

export interface AnalysisRun {
  id: number;
  childProfileId: number;
  status: AnalysisRunStatus;
  overallSummary: string | null;
  crossDocSynthesis: CrossDocSynthesis | null;
  overallRedFlags: RedFlag[];
  advocacyGapAnalysis: AdvocacyGapAnalysis | null;
  parentGoalsSnapshot: ParentGoalSnapshot[];
  sources: AnalysisRunSource[];
  sections: AnalysisRunSection[];
  errorMessage: string | null;
  createdAt: string;
}

export interface CreateAnalysisRunRequest {
  sources: { sourceType: AnalysisSourceType; sourceId: number }[];
}

/** One other source included in a run returned by the `latest` endpoint — for
 * the "part of a larger analysis" note on a document page. */
export interface AnalysisRunOtherSource {
  sourceType: string;
  sourceId: number;
  label: string | null;
}

/** The `GET .../analysis-runs/latest` response: the full run plus the run's
 * other sources and whether it is stale for the document that was queried. */
export interface AnalysisRunLatest extends AnalysisRun {
  otherSources: AnalysisRunOtherSource[];
  stale: boolean;
}

export const TERMINAL_STATUSES: ReadonlySet<AnalysisRunStatus> = new Set([
  "Completed",
  "Error",
]);

export function isTerminalStatus(status: AnalysisRunStatus): boolean {
  return TERMINAL_STATUSES.has(status);
}

export interface SourceOption {
  sourceType: AnalysisSourceType;
  sourceId: number;
  label: string;
}

export function sourceKey(type: AnalysisSourceType, id: number): string {
  return `${type}:${id}`;
}
