import { useEffect, useState } from 'react';
import { Calendar } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { apiErrorMessage } from '@/lib/api-error';
import { listChildMeetings, rsvpToMeeting } from '@/features/meetings/api/meetings-api';
import { formatMeetingWhen } from '@/features/meetings/lib/meeting-time';
import { INVITE_STATUS_LABELS } from '@/features/meetings/types';
import type { InviteStatus, MeetingDto } from '@/features/meetings/types';

function nextUpcoming(meetings: MeetingDto[]): MeetingDto | null {
  const now = Date.now();
  return (
    meetings
      .filter(
        (m) => (m.status === 'Scheduled' || m.status === 'Proposed') && new Date(m.startsAtUtc).getTime() >= now
      )
      .sort((a, b) => new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime())[0] ?? null
  );
}

const inviteBadgeVariant: Record<InviteStatus, 'success' | 'error' | 'warning' | 'neutral'> = {
  Accepted: 'success',
  Declined: 'error',
  Tentative: 'warning',
  Pending: 'neutral',
};

/** Parent child-overview card: the child's next scheduled meeting with
 * Accept/Decline/Tentative RSVP buttons. Renders nothing if there is none. */
export function UpcomingMeetingCard({ childId }: { childId: number }) {
  // `undefined` = loading, `null` = loaded with nothing upcoming.
  const [meeting, setMeeting] = useState<MeetingDto | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);
  const [responding, setResponding] = useState<InviteStatus | null>(null);
  // Bumped by the "Try again" button to re-run the load effect below.
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await listChildMeetings(childId);
        if (!active) return;
        if (response.success && response.data) {
          setMeeting(nextUpcoming(response.data));
          setError(null);
        } else {
          setError(response.message ?? 'Could not load upcoming meetings');
        }
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load upcoming meetings'));
      }
    })();
    return () => {
      active = false;
    };
  }, [childId, retryToken]);

  const handleRsvp = async (status: InviteStatus) => {
    if (!meeting) return;
    setResponding(status);
    try {
      const response = await rsvpToMeeting(meeting.id, { status });
      if (response.success && response.data) setMeeting(response.data);
      else setError(response.message ?? 'Could not record your response');
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not record your response'));
    } finally {
      setResponding(null);
    }
  };

  if (error) {
    return (
      <Card data-testid="upcoming-meeting-error">
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button size="sm" variant="secondary" onClick={() => setRetryToken((t) => t + 1)}>
              Try again
            </Button>
          </Notice>
        </div>
      </Card>
    );
  }

  if (meeting === undefined || meeting === null) return null;

  return (
    <Card data-testid="upcoming-meeting-card">
      <div className="mb-2 flex items-center gap-2">
        <Calendar className="h-4 w-4 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
        <h2 className="font-serif text-base text-brand-slate-800">Upcoming meeting</h2>
      </div>
      <p className="font-medium text-brand-slate-800">{meeting.title || meeting.type}</p>
      <p className="text-sm text-brand-slate-600">{formatMeetingWhen(meeting.startsAtUtc, meeting.durationMinutes)}</p>
      {meeting.location && <p className="text-sm text-brand-slate-500">{meeting.location}</p>}

      {meeting.myInviteStatus && (
        <div className="mt-3">
          <Badge variant={inviteBadgeVariant[meeting.myInviteStatus]}>
            {INVITE_STATUS_LABELS[meeting.myInviteStatus]}
          </Badge>
        </div>
      )}

      <div className="mt-3 flex flex-wrap gap-2">
        <Button
          size="sm"
          onClick={() => handleRsvp('Accepted')}
          loading={responding === 'Accepted'}
          disabled={responding !== null}
          data-testid="upcoming-meeting-accept"
        >
          Accept
        </Button>
        <Button
          size="sm"
          variant="secondary"
          onClick={() => handleRsvp('Tentative')}
          loading={responding === 'Tentative'}
          disabled={responding !== null}
          data-testid="upcoming-meeting-tentative"
        >
          Tentative
        </Button>
        <Button
          size="sm"
          variant="danger"
          onClick={() => handleRsvp('Declined')}
          loading={responding === 'Declined'}
          disabled={responding !== null}
          data-testid="upcoming-meeting-decline"
        >
          Decline
        </Button>
      </div>
    </Card>
  );
}
