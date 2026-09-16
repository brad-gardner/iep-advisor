// Mirrors api/IepAssistant.Api/DTOs/Admin/AuditIntegrityRunDto.cs (pilot-gates
// plan, phase 1, decision 1). A run walks `AccessAuditLogs` in Id order
// recomputing the SHA-256 hash chain; `Ok` means every row still matches its
// recorded hash, `Broken` names the first row that doesn't, `Failed` is an
// unexpected error during the walk itself.

export const AUDIT_INTEGRITY_STATUSES = ['Ok', 'Broken', 'Failed'] as const;
export type AuditIntegrityStatus = (typeof AUDIT_INTEGRITY_STATUSES)[number];

export interface AuditIntegrityRunDto {
  id: number;
  startedAt: string;
  completedAt: string | null;
  rowsChecked: number;
  firstBrokenId: number | null;
  status: AuditIntegrityStatus;
  detail: string | null;
}
