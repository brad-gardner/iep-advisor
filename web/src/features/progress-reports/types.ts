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
