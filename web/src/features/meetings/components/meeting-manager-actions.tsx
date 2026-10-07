import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Copy, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { meetingStatusLabel } from '@/lib/meeting-labels';
import { cancelMeeting, meetingIcsUrl, setMeetingStatus } from '../api/meetings-api';
import { SETTABLE_MEETING_STATUSES } from '../types';
import type { MeetingDto, MeetingStatus, SettableMeetingStatus } from '../types';

interface MeetingManagerActionsProps {
  meeting: MeetingDto;
  onUpdated: (meeting: MeetingDto) => void;
  onReschedule: () => void;
}

/** Status change, reschedule trigger, cancel confirmation, and "Copy ICS
 * link" — the manager-only controls on the meeting drawer. */
export function MeetingManagerActions({ meeting, onUpdated, onReschedule }: MeetingManagerActionsProps) {
  const { t } = useTranslation('meetings-staff');
  const { show: showToast } = useToast();
  const [statusSaving, setStatusSaving] = useState(false);
  const [statusError, setStatusError] = useState<string | null>(null);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelling, setCancelling] = useState(false);
  const [cancelError, setCancelError] = useState<string | null>(null);

  const handleStatusChange = async (status: SettableMeetingStatus) => {
    if (status === meeting.status) return;
    setStatusSaving(true);
    setStatusError(null);
    try {
      const response = await setMeetingStatus(meeting.id, { status });
      if (response.success && response.data) {
        onUpdated(response.data);
        showToast({ message: t('drawer.meetingMarkedToast', { status: meetingStatusLabel(status) }), variant: 'success' });
      } else {
        setStatusError(response.message ?? t('drawer.statusUpdateFailed'));
      }
    } catch (err) {
      setStatusError(apiErrorMessage(err, t('drawer.statusUpdateFailed')));
    } finally {
      setStatusSaving(false);
    }
  };

  const handleCancel = async () => {
    setCancelling(true);
    setCancelError(null);
    try {
      const response = await cancelMeeting(meeting.id, { reason: cancelReason.trim() || undefined });
      if (response.success && response.data) {
        onUpdated(response.data);
        setCancelOpen(false);
        showToast({ message: t('drawer.meetingCancelledToast'), variant: 'success' });
      } else {
        setCancelError(response.message ?? t('drawer.cancelFailed'));
      }
    } catch (err) {
      setCancelError(apiErrorMessage(err, t('drawer.cancelFailed')));
    } finally {
      setCancelling(false);
    }
  };

  const handleCopyIcs = async () => {
    const url = `${window.location.origin}${meetingIcsUrl(meeting.id)}`;
    try {
      await navigator.clipboard.writeText(url);
      showToast({ message: t('drawer.icsLinkCopiedToast'), variant: 'success' });
    } catch {
      showToast({ message: t('drawer.couldNotCopyLinkToast'), variant: 'error' });
    }
  };

  const isCancelled = meeting.status === 'Cancelled';

  return (
    <div className="space-y-3 border-t border-brand-slate-100 pt-4">
      <h3 className="text-sm font-medium text-brand-slate-800">{t('drawer.manageHeading')}</h3>

      {statusError && (
        <div role="alert">
          <Notice variant="error" title={statusError} />
        </div>
      )}

      {!isCancelled && (
        <Select
          id="meeting-status-select"
          label={t('drawer.statusLabel')}
          value={
            (SETTABLE_MEETING_STATUSES as readonly MeetingStatus[]).includes(meeting.status)
              ? meeting.status
              : 'Scheduled'
          }
          disabled={statusSaving}
          onChange={(e) => handleStatusChange(e.target.value as SettableMeetingStatus)}
        >
          {SETTABLE_MEETING_STATUSES.map((s) => (
            <option key={s} value={s}>
              {meetingStatusLabel(s)}
            </option>
          ))}
        </Select>
      )}

      <div className="flex flex-wrap gap-2">
        <Button variant="secondary" size="sm" onClick={handleCopyIcs} data-testid="meeting-copy-ics">
          <Copy className="mr-1.5 h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
          {t('drawer.copyIcsButton')}
        </Button>
        {!isCancelled && (
          <Button variant="secondary" size="sm" onClick={onReschedule} data-testid="meeting-reschedule-open">
            <RefreshCw className="mr-1.5 h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
            {t('drawer.rescheduleButton')}
          </Button>
        )}
        {!isCancelled && (
          <Button variant="danger" size="sm" onClick={() => setCancelOpen(true)} data-testid="meeting-cancel-open">
            {t('drawer.cancelMeetingButton')}
          </Button>
        )}
      </div>

      <ConfirmDialog
        open={cancelOpen}
        title={t('drawer.cancelDialogTitle')}
        message={
          <div className="space-y-3">
            <p>{t('drawer.cancelDialogMessage')}</p>
            <RichTextEditor
              id="meeting-cancel-reason"
              label={t('drawer.cancelReasonLabel')}
              value={cancelReason}
              onChange={setCancelReason}
              placeholder={t('drawer.cancelReasonPlaceholder')}
              minRows={2}
            />
          </div>
        }
        confirmLabel={t('drawer.cancelDialogConfirm')}
        loading={cancelling}
        error={cancelError}
        onConfirm={handleCancel}
        onCancel={() => {
          setCancelError(null);
          setCancelOpen(false);
        }}
        data-testid="meeting-cancel-dialog"
      />
    </div>
  );
}
