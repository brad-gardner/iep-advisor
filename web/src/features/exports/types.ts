// Mirrors api/IepAssistant.Api/DTOs/Export/ExportDtos.cs (plan7-contract.md
// "Phase 4", decision 8). District and per-student data export as a background
// job producing a downloadable ZIP.

export const EXPORT_JOB_STATUSES = ['Queued', 'Running', 'Completed', 'Failed'] as const;
export type ExportJobStatus = (typeof EXPORT_JOB_STATUSES)[number];
// Display labels moved to `locales/{en,es}/staff/exports.json`'s
// `page.status.*` (multilingual plan phase 6) — `exports-admin-page.tsx`
// calls `t(\`page.status.${status}\`)` directly rather than through a
// hardcoded English map here.

/** Jobs Queued or Running are still in flight — the admin page polls while any exist. */
export const IN_FLIGHT_EXPORT_STATUSES: ReadonlySet<ExportJobStatus> = new Set(['Queued', 'Running']);

export const EXPORT_SCOPES = ['District', 'Student'] as const;
export type ExportScope = (typeof EXPORT_SCOPES)[number];

export interface ExportJobDto {
  id: number;
  scope: ExportScope;
  districtId: number;
  schoolStudentId: number | null;
  studentName: string | null;
  requestedByUserId: number;
  requestedByName: string | null;
  status: ExportJobStatus;
  requestedAt: string;
  startedAt: string | null;
  completedAt: string | null;
  sizeBytes: number | null;
  error: string | null;
  studentCount: number;
  fileCount: number;
}

export interface ExportJobIdDto {
  jobId: number;
}
