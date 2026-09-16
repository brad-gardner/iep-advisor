import { useState } from 'react';
import { Copy, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Select, Textarea } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { cancelMeeting, meetingIcsUrl, setMeetingStatus } from '../api/meetings-api';
import { MEETING_STATUS_LABELS, SETTABLE_MEETING_STATUSES } from '../types';
import type { MeetingDto, MeetingStatus, SettableMeetingStatus } from '../types';

interface MeetingManagerActionsProps {
  meeting: MeetingDto;
  onUpdated: (meeting: MeetingDto) => void;
  onReschedule: () => void;
}

/** Status change, reschedule trigger, cancel confirmation, and "Copy ICS
 * link" — the manager-only controls on the meeting drawer. */
export function MeetingManagerActions({ meeting, onUpdated, onReschedule }: MeetingManagerActionsProps) {
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
        showToast({ message: `Meeting marked ${MEETING_STATUS_LABELS[status]}`, variant: 'success' });
      } else {
        setStatusError(response.message ?? 'Could not update the status');
      }
    } catch (err) {
      setStatusError(apiErrorMessage(err, 'Could not update the status'));
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
        showToast({ message: 'Meeting cancelled', variant: 'success' });
      } else {
        setCancelError(response.message ?? 'Could not cancel the meeting');
      }
    } catch (err) {
      setCancelError(apiErrorMessage(err, 'Could not cancel the meeting'));
    } finally {
      setCancelling(false);
    }
  };

  const handleCopyIcs = async () => {
    const url = `${window.location.origin}${meetingIcsUrl(meeting.id)}`;
    try {
      await navigator.clipboard.writeText(url);
      showToast({ message: 'ICS link copied', variant: 'success' });
    } catch {
      showToast({ message: 'Could not copy the link', variant: 'error' });
    }
  };

  const isCancelled = meeting.status === 'Cancelled';

  return (
    <div className="space-y-3 border-t border-brand-slate-100 pt-4">
      <h3 className="text-sm font-medium text-brand-slate-800">Manage</h3>

      {statusError && (
        <div role="alert">
          <Notice variant="error" title={statusError} />
        </div>
      )}

      {!isCancelled && (
        <Select
          id="meeting-status-select"
          label="Status"
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
              {MEETING_STATUS_LABELS[s]}
            </option>
          ))}
        </Select>
      )}

      <div className="flex flex-wrap gap-2">
        <Button variant="secondary" size="sm" onClick={handleCopyIcs} data-testid="meeting-copy-ics">
          <Copy className="mr-1.5 h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
          Copy ICS link
        </Button>
        {!isCancelled && (
          <Button variant="secondary" size="sm" onClick={onReschedule} data-testid="meeting-reschedule-open">
            <RefreshCw className="mr-1.5 h-3.5 w-3.5" strokeWidth={1.8} aria-hidden="true" />
            Reschedule
          </Button>
        )}
        {!isCancelled && (
          <Button variant="danger" size="sm" onClick={() => setCancelOpen(true)} data-testid="meeting-cancel-open">
            Cancel meeting
          </Button>
        )}
      </div>

      <ConfirmDialog
        open={cancelOpen}
        title="Cancel meeting"
        message={
          <div className="space-y-3">
            <p>Participants will be notified. This cannot be undone.</p>
            <Textarea
              id="meeting-cancel-reason"
              label="Reason"
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              placeholder="Optional"
              rows={2}
            />
          </div>
        }
        confirmLabel="Cancel meeting"
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
