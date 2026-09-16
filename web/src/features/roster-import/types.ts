// Mirrors api/IepAssistant.Api/DTOs/District/Import*.cs (plan 3 contract).
// Enums serialize as their string names.

export type ImportKind = 'Students' | 'Staff';
export type ImportBatchStatus = 'Previewed' | 'Committing' | 'Committed' | 'Discarded';
export type ImportRowOutcome = 'New' | 'Updated' | 'Unchanged' | 'Error';

export interface ImportCounts {
  total: number;
  new: number;
  updated: number;
  unchanged: number;
  error: number;
}

export interface ImportRow {
  rowNumber: number;
  outcome: ImportRowOutcome;
  // StudentId for a Students import, Email for a Staff import.
  key: string;
  displayName: string;
  message: string | null;
  // Human-readable diffs, e.g. "Grade: 6 → 7".
  changes: string[];
}

export interface ImportPreview {
  batchId: number;
  kind: ImportKind;
  fileName: string;
  status: ImportBatchStatus;
  counts: ImportCounts;
  // All rows, in file order.
  rows: ImportRow[];
  createdAt: string;
  committedAt: string | null;
}

export interface ImportResult {
  batchId: number;
  committed: { new: number; updated: number; unchanged: number };
  skipped: number;
  status: ImportBatchStatus;
}

export interface ImportBatch {
  batchId: number;
  kind: ImportKind;
  fileName: string;
  status: ImportBatchStatus;
  counts: ImportCounts;
  createdAt: string;
  committedAt: string | null;
  createdByName: string;
}

export interface CommitImportRequest {
  // True: import the valid rows and skip error rows. False with any error rows
  // is refused by the server ("Fix the errors or choose to import valid rows only.").
  commitValid: boolean;
}

// Client-side pre-check mirroring the server cap.
export const MAX_IMPORT_FILE_BYTES = 5 * 1024 * 1024;
