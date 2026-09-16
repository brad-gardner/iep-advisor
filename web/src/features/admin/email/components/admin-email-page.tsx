import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Select } from '@/components/ui/input';
import { Table, type TableColumn } from '@/components/ui/table';
import { useToast } from '@/components/ui/toast';
import type { MenuItem } from '@/components/ui/menu';
import { usePageTitle } from '@/hooks/use-page-title';
import { apiErrorMessage } from '@/lib/api-error';
import { cancelOutboundEmail, resendOutboundEmail } from '../api/email-admin-api';
import { useOutboundEmails } from '../hooks/use-outbound-emails';
import type { OutboundEmailDto, OutboundEmailStatus, OutboundEmailStatusFilter } from '../types';

const FILTER_OPTIONS: OutboundEmailStatusFilter[] = ['Failed', 'Queued', 'Sent', 'All'];

const STATUS_VARIANT: Record<OutboundEmailStatus, 'neutral' | 'warning' | 'success' | 'error'> = {
  Queued: 'neutral',
  Sending: 'warning',
  Sent: 'success',
  Failed: 'error',
  Cancelled: 'neutral',
};

const LAST_ERROR_TRUNCATE_LENGTH = 60;

function truncate(text: string, max: number): string {
  return text.length > max ? `${text.slice(0, max - 1)}…` : text;
}

interface ConfirmTarget {
  email: OutboundEmailDto;
  action: 'resend' | 'cancel';
}

/** Platform admin `/admin/email`: every outbound email the app has queued —
 *  failures visible and resendable, in-flight ones cancellable (pilot-gates
 *  plan, phase 1, decision 2). Polls while any row is Queued/Sending. */
export function AdminEmailPage() {
  usePageTitle('Outbound email');
  const [filter, setFilter] = useState<OutboundEmailStatusFilter>('Failed');
  const { emails, status, isLoading, error, reload } = useOutboundEmails(filter);
  const { show: showToast } = useToast();

  const [confirmTarget, setConfirmTarget] = useState<ConfirmTarget | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [confirmError, setConfirmError] = useState<string | null>(null);

  const closeConfirm = () => {
    setConfirmTarget(null);
    setConfirmError(null);
  };

  const handleConfirm = async () => {
    if (!confirmTarget) return;
    const { email, action } = confirmTarget;
    setIsSubmitting(true);
    setConfirmError(null);
    try {
      const res = action === 'resend' ? await resendOutboundEmail(email.id) : await cancelOutboundEmail(email.id);
      if (res.success) {
        showToast({
          message: action === 'resend' ? `Email to ${email.toEmail} re-queued` : `Email to ${email.toEmail} cancelled`,
          variant: 'success',
        });
        setConfirmTarget(null);
        reload();
      } else {
        setConfirmError(res.message ?? `Could not ${action} this email.`);
      }
    } catch (err) {
      setConfirmError(apiErrorMessage(err, `Could not ${action} this email.`));
    } finally {
      setIsSubmitting(false);
    }
  };

  const columns: TableColumn<OutboundEmailDto>[] = [
    { key: 'to', header: 'To', cell: (e) => e.toEmail, sortValue: (e) => e.toEmail },
    { key: 'subject', header: 'Subject', cell: (e) => e.subject, hideBelow: 'md' },
    { key: 'kind', header: 'Kind', cell: (e) => e.kind, hideBelow: 'lg' },
    {
      key: 'status',
      header: 'Status',
      cell: (e) => (
        <Badge variant={STATUS_VARIANT[e.status]} data-testid={`email-status-${e.id}`}>
          {e.status}
        </Badge>
      ),
      sortValue: (e) => e.status,
    },
    { key: 'attempts', header: 'Attempts', align: 'right', cell: (e) => e.attempts, hideBelow: 'lg' },
    {
      key: 'lastError',
      header: 'Last error',
      cell: (e) =>
        e.lastError ? (
          <span title={e.lastError} className="text-brand-danger-700">
            {truncate(e.lastError, LAST_ERROR_TRUNCATE_LENGTH)}
          </span>
        ) : (
          '—'
        ),
    },
    {
      key: 'when',
      header: 'Next attempt / sent',
      align: 'right',
      cell: (e) =>
        e.sentAt
          ? `Sent ${new Date(e.sentAt).toLocaleString()}`
          : e.status === 'Cancelled'
            ? '—'
            : `Next: ${new Date(e.nextAttemptAt).toLocaleString()}`,
      sortValue: (e) => e.sentAt ?? e.nextAttemptAt,
    },
  ];

  const rowActions = (email: OutboundEmailDto): MenuItem[] => {
    const actions: MenuItem[] = [];
    if (email.status === 'Failed' || email.status === 'Cancelled') {
      actions.push({
        label: 'Resend',
        onSelect: () => setConfirmTarget({ email, action: 'resend' }),
        'data-testid': `email-resend-${email.id}`,
      });
    }
    if (email.status === 'Queued') {
      actions.push({
        label: 'Cancel',
        variant: 'danger',
        onSelect: () => setConfirmTarget({ email, action: 'cancel' }),
        'data-testid': `email-cancel-${email.id}`,
      });
    }
    return actions;
  };

  return (
    <PageLayout title="Outbound email" subtitle="Every email the app has queued, sent, or failed to send.">
      {status && !status.configured && (
        <div role="alert">
          <Notice variant="warning" title="Email delivery is not configured" data-testid="email-unconfigured-banner">
            No email will be delivered until Azure Communication Services is configured for this environment.
          </Notice>
        </div>
      )}
      {status?.configured && (
        <p className="text-sm text-brand-slate-500" data-testid="email-status-summary">
          {status.queued} queued · {status.sending} sending · {status.failed} failed
          {status.lastSentAt ? ` · last sent ${new Date(status.lastSentAt).toLocaleString()}` : ''}
        </p>
      )}

      <Select
        label="Status"
        value={filter}
        onChange={(e) => setFilter(e.target.value as OutboundEmailStatusFilter)}
        data-testid="email-status-filter"
        className="max-w-xs"
      >
        {FILTER_OPTIONS.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </Select>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <Table
        label="Outbound emails"
        data-testid="outbound-emails-table"
        columns={columns}
        rows={emails}
        rowKey={(e) => e.id}
        rowActions={rowActions}
        rowActionLabel={(e) => e.toEmail}
        loading={isLoading}
        defaultSort={{ key: 'when', direction: 'desc' }}
        empty={<p className="text-center text-sm text-brand-slate-400">No emails match this filter.</p>}
      />

      <ConfirmDialog
        open={confirmTarget !== null}
        title={confirmTarget?.action === 'resend' ? 'Resend email' : 'Cancel email'}
        message={
          confirmTarget?.action === 'resend'
            ? `Re-queue this email to ${confirmTarget.email.toEmail} for immediate delivery?`
            : `Cancel this queued email to ${confirmTarget?.email.toEmail}? It will never be sent.`
        }
        confirmLabel={confirmTarget?.action === 'resend' ? 'Resend' : 'Cancel email'}
        cancelLabel="Keep as is"
        confirmVariant={confirmTarget?.action === 'resend' ? 'primary' : 'danger'}
        loading={isSubmitting}
        error={confirmError}
        onConfirm={handleConfirm}
        onCancel={closeConfirm}
        data-testid="email-action-confirm"
      />
    </PageLayout>
  );
}
