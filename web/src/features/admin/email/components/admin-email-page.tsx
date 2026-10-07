import { useState } from 'react';
import { useTranslation } from 'react-i18next';
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
import { getActiveLanguage } from '@/lib/i18n/format';
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

/** Full date+time (not just date), formatted in the active i18next language
 *  — same local-helper shape as `AdminAuditPage`'s own `formatDateTime`;
 *  `lib/format-date.ts`'s `formatDate` only covers a date, not a timestamp. */
function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(getActiveLanguage());
}

interface ConfirmTarget {
  email: OutboundEmailDto;
  action: 'resend' | 'cancel';
}

/** Platform admin `/admin/email`: every outbound email the app has queued —
 *  failures visible and resendable, in-flight ones cancellable (pilot-gates
 *  plan, phase 1, decision 2). Polls while any row is Queued/Sending. */
export function AdminEmailPage() {
  const { t } = useTranslation(['admin', 'common']);
  usePageTitle(t('email.pageTitle'));
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
    const actionFailedFallback =
      action === 'resend' ? t('email.resendFailedFallback') : t('email.cancelFailedFallback');
    try {
      const res = action === 'resend' ? await resendOutboundEmail(email.id) : await cancelOutboundEmail(email.id);
      if (res.success) {
        showToast({
          message:
            action === 'resend'
              ? t('email.resentToast', { email: email.toEmail })
              : t('email.cancelledToast', { email: email.toEmail }),
          variant: 'success',
        });
        setConfirmTarget(null);
        reload();
      } else {
        setConfirmError(res.message ?? actionFailedFallback);
      }
    } catch (err) {
      setConfirmError(apiErrorMessage(err, actionFailedFallback));
    } finally {
      setIsSubmitting(false);
    }
  };

  const columns: TableColumn<OutboundEmailDto>[] = [
    { key: 'to', header: t('email.column.to'), cell: (e) => e.toEmail, sortValue: (e) => e.toEmail },
    { key: 'subject', header: t('email.column.subject'), cell: (e) => e.subject, hideBelow: 'md' },
    { key: 'kind', header: t('email.column.kind'), cell: (e) => e.kind, hideBelow: 'lg' },
    {
      key: 'status',
      header: t('common.column.status'),
      cell: (e) => (
        <Badge variant={STATUS_VARIANT[e.status]} data-testid={`email-status-${e.id}`}>
          {t(`email.status.${e.status}`)}
        </Badge>
      ),
      sortValue: (e) => e.status,
    },
    { key: 'attempts', header: t('email.column.attempts'), align: 'right', cell: (e) => e.attempts, hideBelow: 'lg' },
    {
      key: 'lastError',
      header: t('email.column.lastError'),
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
      header: t('email.column.when'),
      align: 'right',
      cell: (e) =>
        e.sentAt
          ? t('email.sentAt', { when: formatDateTime(e.sentAt) })
          : e.status === 'Cancelled'
            ? '—'
            : t('email.nextAttempt', { when: formatDateTime(e.nextAttemptAt) }),
      sortValue: (e) => e.sentAt ?? e.nextAttemptAt,
    },
  ];

  const rowActions = (email: OutboundEmailDto): MenuItem[] => {
    const actions: MenuItem[] = [];
    if (email.status === 'Failed' || email.status === 'Cancelled') {
      actions.push({
        label: t('email.resend'),
        onSelect: () => setConfirmTarget({ email, action: 'resend' }),
        'data-testid': `email-resend-${email.id}`,
      });
    }
    if (email.status === 'Queued') {
      actions.push({
        label: t('email.cancel'),
        variant: 'danger',
        onSelect: () => setConfirmTarget({ email, action: 'cancel' }),
        'data-testid': `email-cancel-${email.id}`,
      });
    }
    return actions;
  };

  return (
    <PageLayout title={t('email.pageTitle')} subtitle={t('email.subtitle')}>
      {status && !status.configured && (
        <div role="alert">
          <Notice variant="warning" title={t('email.unconfiguredTitle')} data-testid="email-unconfigured-banner">
            {t('email.unconfiguredMessage')}
          </Notice>
        </div>
      )}
      {status?.configured && (
        <p className="text-sm text-brand-slate-500" data-testid="email-status-summary">
          {status.lastSentAt
            ? t('email.statusSummaryLastSent', {
                queued: status.queued,
                sending: status.sending,
                failed: status.failed,
                when: formatDateTime(status.lastSentAt),
              })
            : t('email.statusSummary', { queued: status.queued, sending: status.sending, failed: status.failed })}
        </p>
      )}

      <Select
        label={t('email.statusFilterLabel')}
        value={filter}
        onChange={(e) => setFilter(e.target.value as OutboundEmailStatusFilter)}
        data-testid="email-status-filter"
        className="max-w-xs"
      >
        {FILTER_OPTIONS.map((option) => (
          <option key={option} value={option}>
            {t(`email.filter.${option}`)}
          </option>
        ))}
      </Select>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <Table
        label={t('email.tableLabel')}
        data-testid="outbound-emails-table"
        columns={columns}
        rows={emails}
        rowKey={(e) => e.id}
        rowActions={rowActions}
        rowActionLabel={(e) => e.toEmail}
        loading={isLoading}
        defaultSort={{ key: 'when', direction: 'desc' }}
        empty={<p className="text-center text-sm text-brand-slate-500">{t('email.noneMatch')}</p>}
      />

      <ConfirmDialog
        open={confirmTarget !== null}
        title={confirmTarget?.action === 'resend' ? t('email.resendConfirmTitle') : t('email.cancelConfirmTitle')}
        message={
          confirmTarget?.action === 'resend'
            ? t('email.resendConfirmMessage', { email: confirmTarget.email.toEmail })
            : t('email.cancelConfirmMessage', { email: confirmTarget?.email.toEmail })
        }
        confirmLabel={confirmTarget?.action === 'resend' ? t('email.resend') : t('email.cancelConfirmTitle')}
        cancelLabel={t('email.keepAsIs')}
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
