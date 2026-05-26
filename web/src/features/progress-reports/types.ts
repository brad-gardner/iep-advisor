export type ProgressReportStatus =
  | "created"
  | "uploaded"
  | "processing"
  | "parsed"
  | "error";

export interface ProgressReport {
  id: number;
  iepDocumentId: number;
  childProfileId: number;
  fileName: string | null;
  uploadDate: string;
  reportingPeriodStart: string | null;
  reportingPeriodEnd: string | null;
  notes: string | null;
  status: string;
  errorMessage: string | null;
  fileSizeBytes: number;
  createdAt: string;
}

export interface CreateProgressReportRequest {
  reportingPeriodStart?: string | null;
  reportingPeriodEnd?: string | null;
  notes?: string;
}

export type ProgressRating =
  | "met"
  | "on_track"
  | "concerning"
  | "regressing"
  | "insufficient_data"
  | string;

export type EvidenceQuality = "strong" | "adequate" | "weak" | string;

export interface GoalProgressFinding {
  iepGoalText: string;
  iepGoalId: number | null;
  domain: string | null;
  reportedProgress: string;
  progressRating: ProgressRating;
  evidenceQuality: EvidenceQuality;
  redFlags: string[];
  parentTalkingPoints: string[];
}

export interface ProgressReportRedFlag {
  severity: "high" | "medium" | "low" | string;
  category: string;
  finding: string;
  whyItMatters: string;
}

export interface IepGoalSnapshot {
  id: number | null;
  goalText: string;
  domain: string | null;
}

export type AnalysisStatus = "pending" | "analyzing" | "completed" | "error";

export interface ProgressReportAnalysis {
  id: number;
  progressReportId: number;
  status: AnalysisStatus;
  summary: string | null;
  goalProgressFindings: GoalProgressFinding[];
  redFlags: ProgressReportRedFlag[];
  advocacyGapAnalysis: import("@/types/api").AdvocacyGapAnalysis | null;
  parentGoalsSnapshot: import("@/types/api").ParentGoalSnapshot[] | null;
  iepGoalsSnapshot: IepGoalSnapshot[];
  errorMessage: string | null;
  createdAt: string;
}
