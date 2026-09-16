// Mirrors api/IepAssistant.Api/DTOs/Admin/OutboundEmailDto.cs (pilot-gates plan,
// phase 1, decision 2). Platform-admin visibility into every queued/sent
// email — `EmailService` no longer swallows send failures; every caller
// enqueues onto `OutboundEmails`, drained by `OutboundEmailWorker`.

export const OUTBOUND_EMAIL_STATUSES = ['Queued', 'Sending', 'Sent', 'Failed', 'Cancelled'] as const;
export type OutboundEmailStatus = (typeof OUTBOUND_EMAIL_STATUSES)[number];

/** The page's status filter — `All` has no server-side enum counterpart. */
export type OutboundEmailStatusFilter = OutboundEmailStatus | 'All';

/** Rows in `Queued`/`Sending` are still in flight — the admin page polls while any exist. */
export const IN_FLIGHT_EMAIL_STATUSES: ReadonlySet<OutboundEmailStatus> = new Set(['Queued', 'Sending']);

export interface OutboundEmailDto {
  id: number;
  toEmail: string;
  subject: string;
  kind: string;
  status: OutboundEmailStatus;
  attempts: number;
  lastError: string | null;
  nextAttemptAt: string;
  sentAt: string | null;
  correlationId: string | null;
  createdAt: string;
}

export interface OutboundEmailStatusDto {
  configured: boolean;
  queued: number;
  failed: number;
  lastSentAt: string | null;
}
