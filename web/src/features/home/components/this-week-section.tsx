import { Badge } from '@/components/ui/badge';
import { formatMeetingWhen } from '@/features/meetings/lib/meeting-time';
import { MEETING_STATUS_LABELS } from '@/features/meetings/types';
import type { MeetingStatus } from '@/features/meetings/types';
import { EmptyHint } from './empty-hint';
import { HomeSection } from './home-section';
import { WorkItemRow } from './work-item-row';
import type { HomeMeetingDto } from '../types';

const statusBadgeVariant: Record<MeetingStatus, 'success' | 'error' | 'warning' | 'neutral'> = {
  Proposed: 'neutral',
  Scheduled: 'success',
  Held: 'success',
  Continued: 'warning',
  Cancelled: 'error',
};

interface ThisWeekSectionProps {
  title: string;
  subtitle?: string;
  meetings: HomeMeetingDto[];
  /** SchoolAdmin/DistrictAdmin variant: each meeting also shows a disabled
   * "brief" affordance (plan 7 supplies the real link). */
  showBriefNote?: boolean;
  'data-testid'?: string;
}

/** Meetings the viewer is on this week (staff variants) or every scheduled
 * meeting in the admin's building/district this week (admin variants). Every
 * row links to the student since there is no per-meeting detail route. */
export function ThisWeekSection({
  title,
  subtitle,
  meetings,
  showBriefNote = false,
  'data-testid': testId = 'home-this-week',
}: ThisWeekSectionProps) {
  return (
    <HomeSection title={title} subtitle={subtitle} data-testid={testId}>
      {meetings.length === 0 ? (
        <EmptyHint data-testid={`${testId}-empty`}>Nothing scheduled this week.</EmptyHint>
      ) : (
        <ul className="divide-y divide-brand-slate-100">
          {meetings.map((meeting) => (
            <WorkItemRow
              key={meeting.id}
              title={meeting.studentName}
              subtitle={`${meeting.title || meeting.type} · ${formatMeetingWhen(meeting.startsAtUtc, meeting.durationMinutes)}`}
              href={`/educator/students/${meeting.studentId}`}
              data-testid={`${testId}-${meeting.id}`}
              meta={
                <>
                  <Badge variant={statusBadgeVariant[meeting.status]}>
                    {MEETING_STATUS_LABELS[meeting.status]}
                  </Badge>
                  {showBriefNote && (
                    <Badge variant="neutral" title="Meeting briefs are coming soon">
                      Brief · coming soon
                    </Badge>
                  )}
                </>
              }
            />
          ))}
        </ul>
      )}
    </HomeSection>
  );
}
