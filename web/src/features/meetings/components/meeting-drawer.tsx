import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Drawer } from '@/components/ui/drawer';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { recordAttendance, rsvpToMeeting } from '../api/meetings-api';
import { formatMeetingWhen, timeZoneLabel } from '../lib/meeting-time';
import { INVITE_STATUS_LABELS, MEETING_STATUS_LABELS, MEETING_TYPE_LABELS } from '../types';
import type { AttendanceEntry, InviteStatus, MeetingDto } from '../types';
import { MeetingManagerActions } from './meeting-manager-actions';
import { ParticipantList } from './participant-list';
import { ScheduleMeetingModal } from './schedule-meeting-modal';

interface MeetingDrawerProps {
  open: boolean;
  meeting: MeetingDto | null;
  onClose: () => void;
  onUpdated: (meeting: MeetingDto) => void;
}

const statusBadgeVariant: Record<MeetingDto['status'], 'success' | 'error' | 'warning' | 'neutral'> = {
  Proposed: 'neutral',
  Scheduled: 'success',
  Held: 'success',
  Continued: 'warning',
  Cancelled: 'error',
};

function initialAttendance(meeting: MeetingDto): Map<number, AttendanceEntry> {
  return new Map(
    meeting.participants.map((p) => [
      p.id,
      { participantId: p.id, attended: p.attended ?? false, excusalNote: p.excusalNote ?? undefined },
    ])
  );
}

function canCaptureAttendanceFor(meeting: MeetingDto): boolean {
  return meeting.canManage && (meeting.status === 'Held' || meeting.status === 'Continued');
}

/**
 * Meeting detail: schedule + location, the participant roster with RSVP
 * status, my own RSVP buttons, attendance capture (Held/Continued only), and
 * manager-only status/reschedule/cancel/ICS controls.
 */
export function MeetingDrawer({ open, meeting, onClose, onUpdated }: MeetingDrawerProps) {
  const { show: showToast } = useToast();
  const [rsvpSaving, setRsvpSaving] = useState<InviteStatus | null>(null);
  const [rsvpError, setRsvpError] = useState<string | null>(null);
  // Lazily seeded from the meeting mounted first, so attendance capture is
  // available from the very first render (not just after a later change).
  const [attendance, setAttendance] = useState<Map<number, AttendanceEntry> | null>(() =>
    meeting && canCaptureAttendanceFor(meeting) ? initialAttendance(meeting) : null
  );
  const [attendanceSaving, setAttendanceSaving] = useState(false);
  const [attendanceError, setAttendanceError] = useState<string | null>(null);
  const [rescheduling, setRescheduling] = useState(false);
  // The parent keeps one `MeetingDrawer` mounted and swaps `meeting` as the
  // selection changes, so per-meeting local state (attendance draft, RSVP/
  // attendance errors, an in-progress reschedule) is reset whenever the
  // meeting id changes — computed during render (see `roster-filters.tsx` for
  // the same "adjust state in response to a prop change" idiom), not via an
  // effect.
  const [seenMeetingId, setSeenMeetingId] = useState<number | null>(meeting?.id ?? null);

  if (!meeting) return null;

  const canCaptureAttendance = canCaptureAttendanceFor(meeting);

  if (meeting.id !== seenMeetingId) {
    setSeenMeetingId(meeting.id);
    setAttendance(canCaptureAttendance ? initialAttendance(meeting) : null);
    setRsvpError(null);
    setAttendanceError(null);
    setRescheduling(false);
  }

  const handleRsvp = async (status: InviteStatus) => {
    setRsvpSaving(status);
    setRsvpError(null);
    try {
      const response = await rsvpToMeeting(meeting.id, { status });
      if (response.success && response.data) {
        onUpdated(response.data);
        showToast({ message: 'Response recorded', variant: 'success' });
      } else {
        setRsvpError(response.message ?? 'Could not record your response');
      }
    } catch (err) {
      setRsvpError(apiErrorMessage(err, 'Could not record your response'));
    } finally {
      setRsvpSaving(null);
    }
  };

  const handleAttendanceChange = (participantId: number, attended: boolean, excusalNote?: string) => {
    setAttendance((prev) => {
      const next = new Map(prev ?? []);
      next.set(participantId, { participantId, attended, excusalNote });
      return next;
    });
  };

  const handleSaveAttendance = async () => {
    if (!attendance) return;
    setAttendanceSaving(true);
    setAttendanceError(null);
    try {
      const response = await recordAttendance(meeting.id, {
        attendance: Array.from(attendance.values()),
      });
      if (response.success && response.data) {
        onUpdated(response.data);
        showToast({ message: 'Attendance saved', variant: 'success' });
      } else {
        setAttendanceError(response.message ?? 'Could not save attendance');
      }
    } catch (err) {
      setAttendanceError(apiErrorMessage(err, 'Could not save attendance'));
    } finally {
      setAttendanceSaving(false);
    }
  };

  return (
    <>
      <Drawer
        open={open && !rescheduling}
        onClose={onClose}
        title={meeting.title || MEETING_TYPE_LABELS[meeting.type]}
        size="lg"
        data-testid="meeting-drawer"
      >
        <div className="space-y-5">
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="text-sm text-brand-slate-600">{MEETING_TYPE_LABELS[meeting.type]}</p>
              <p className="mt-1 text-sm font-medium text-brand-slate-800">
                {formatMeetingWhen(meeting.startsAtUtc, meeting.durationMinutes)}
              </p>
              <p className="text-xs text-brand-slate-400">
                Scheduled in {timeZoneLabel(meeting.timeZoneId)}
              </p>
              {meeting.location && <p className="mt-1 text-sm text-brand-slate-600">{meeting.location}</p>}
              {meeting.videoUrl && (
                <a
                  href={meeting.videoUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-1 block text-sm text-brand-teal-600 underline"
                >
                  Join video call
                </a>
              )}
              <Link
                to={`/educator/students/${meeting.schoolStudentId}`}
                className="mt-1 block text-sm text-brand-slate-500 underline"
              >
                {meeting.studentName}
              </Link>
            </div>
            <Badge variant={statusBadgeVariant[meeting.status]}>{MEETING_STATUS_LABELS[meeting.status]}</Badge>
          </div>

          {meeting.myInviteStatus && meeting.status !== 'Cancelled' && (
            <div className="space-y-2 rounded-card border border-brand-slate-200 p-3">
              <p className="text-sm text-brand-slate-600">
                Your response: <strong>{INVITE_STATUS_LABELS[meeting.myInviteStatus]}</strong>
              </p>
              {rsvpError && (
                <div role="alert">
                  <Notice variant="error" title={rsvpError} />
                </div>
              )}
              <div className="flex flex-wrap gap-2">
                <Button size="sm" onClick={() => handleRsvp('Accepted')} loading={rsvpSaving === 'Accepted'} data-testid="meeting-rsvp-accept">
                  Accept
                </Button>
                <Button size="sm" variant="secondary" onClick={() => handleRsvp('Tentative')} loading={rsvpSaving === 'Tentative'} data-testid="meeting-rsvp-tentative">
                  Tentative
                </Button>
                <Button size="sm" variant="danger" onClick={() => handleRsvp('Declined')} loading={rsvpSaving === 'Declined'} data-testid="meeting-rsvp-decline">
                  Decline
                </Button>
              </div>
            </div>
          )}

          <div>
            <h3 className="mb-2 text-sm font-medium text-brand-slate-800">Participants</h3>
            <ParticipantList
              participants={meeting.participants}
              attendance={
                canCaptureAttendance && attendance
                  ? { entries: attendance, onChange: handleAttendanceChange }
                  : undefined
              }
            />
            {canCaptureAttendance && (
              <div className="mt-3 space-y-2">
                {attendanceError && (
                  <div role="alert">
                    <Notice variant="error" title={attendanceError} />
                  </div>
                )}
                <Button size="sm" onClick={handleSaveAttendance} loading={attendanceSaving} data-testid="meeting-save-attendance">
                  Save attendance
                </Button>
              </div>
            )}
          </div>

          {meeting.canManage && (
            <MeetingManagerActions
              meeting={meeting}
              onUpdated={onUpdated}
              onReschedule={() => setRescheduling(true)}
            />
          )}
        </div>
      </Drawer>

      {meeting.canManage && (
        <ScheduleMeetingModal
          open={rescheduling}
          onClose={() => setRescheduling(false)}
          studentId={meeting.schoolStudentId}
          studentName={meeting.studentName}
          meeting={meeting}
          onSaved={(updated) => {
            onUpdated(updated);
            setRescheduling(false);
          }}
        />
      )}
    </>
  );
}
