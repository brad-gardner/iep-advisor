import { useState } from 'react';
import { Calendar } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { apiErrorMessage } from '@/lib/api-error';
import { rsvpToMeeting } from '@/features/meetings/api/meetings-api';
import { RsvpButtonGroup } from '@/features/meetings/components/rsvp-button-group';
import { formatMeetingWhen } from '@/features/meetings/lib/meeting-time';
import { INVITE_STATUS_LABELS } from '@/features/meetings/types';
import type { InviteStatus } from '@/features/meetings/types';
import type { HomeMeetingDto } from '../types';

function countdownLabel(daysUntil: number): string {
  if (daysUntil <= 0) return 'Today';
  if (daysUntil === 1) return 'Tomorrow';
  return `In ${daysUntil} days`;
}

interface NextMeetingCardProps {
  meeting: HomeMeetingDto;
  /** Precomputed by the server for the parent variant; omit for the student
   * variant (its own next meeting has no separate countdown in the contract). */
  daysUntil?: number;
  /** Whose meeting this is, e.g. a child's name for the parent variant. */
  subtitle?: string;
  /** Reflects a successful RSVP back into the caller's home state. */
  onUpdated?: (meeting: HomeMeetingDto) => void;
  'data-testid'?: string;
}

/** The parent/student home "Next meeting" card: countdown, RSVP (reusing the
 * same button group as the family's upcoming-meeting card), and the meeting's
 * basic details. Self-contained RSVP state, like `UpcomingMeetingCard`. */
export function NextMeetingCard({
  meeting,
  daysUntil,
  subtitle,
  onUpdated,
  'data-testid': testId = 'next-meeting-card',
}: NextMeetingCardProps) {
  const [pending, setPending] = useState<InviteStatus | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleRespond = async (status: InviteStatus) => {
    setPending(status);
    setError(null);
    try {
      const response = await rsvpToMeeting(meeting.id, { status });
      if (response.success && response.data) {
        onUpdated?.({ ...meeting, myInviteStatus: response.data.myInviteStatus });
      } else {
        setError(response.message ?? 'Could not record your response');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not record your response'));
    } finally {
      setPending(null);
    }
  };

  return (
    <Card data-testid={testId}>
      <div className="mb-2 flex flex-wrap items-center gap-2">
        <Calendar className="h-4 w-4 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
        <h2 className="font-serif text-base text-brand-slate-800">Next meeting</h2>
        {daysUntil != null && (
          <Badge variant={daysUntil <= 3 ? 'warning' : 'neutral'}>{countdownLabel(daysUntil)}</Badge>
        )}
      </div>
      {subtitle && <p className="text-sm text-brand-slate-500">{subtitle}</p>}
      <p className="font-medium text-brand-slate-800">{meeting.title || meeting.type}</p>
      <p className="text-sm text-brand-slate-600">
        {formatMeetingWhen(meeting.startsAtUtc, meeting.durationMinutes)}
      </p>

      {meeting.myInviteStatus && (
        <div className="mt-3">
          <Badge
            variant={
              meeting.myInviteStatus === 'Accepted'
                ? 'success'
                : meeting.myInviteStatus === 'Declined'
                  ? 'error'
                  : meeting.myInviteStatus === 'Tentative'
                    ? 'warning'
                    : 'neutral'
            }
          >
            {INVITE_STATUS_LABELS[meeting.myInviteStatus]}
          </Badge>
        </div>
      )}

      {error && (
        <div role="alert" className="mt-3">
          <Notice variant="error" title={error} />
        </div>
      )}

      <RsvpButtonGroup onRespond={handleRespond} pending={pending} testIdPrefix="next-meeting" />
    </Card>
  );
}
