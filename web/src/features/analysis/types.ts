import type {
  AdvocacyGapAnalysis,
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
}

export interface AnalysisRunSection {
  id: number;
  analysisRunSourceId: number | null;
  sectionKind: string;
  analysis: AnalysisRunSectionAnalysis | null;
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
