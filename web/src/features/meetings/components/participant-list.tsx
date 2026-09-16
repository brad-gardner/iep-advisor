import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { TEAM_ROLE_LABELS } from '@/features/educator/types';
import { INVITE_STATUS_LABELS } from '../types';
import type { AttendanceEntry, InviteStatus, MeetingParticipantDto } from '../types';

const inviteStatusVariant: Record<InviteStatus, 'success' | 'error' | 'warning' | 'neutral'> = {
  Accepted: 'success',
  Declined: 'error',
  Tentative: 'warning',
  Pending: 'neutral',
};

interface ParticipantListProps {
  participants: MeetingParticipantDto[];
  /** Present while the meeting is Held/Continued and the viewer can manage it. */
  attendance?: {
    entries: ReadonlyMap<number, AttendanceEntry>;
    onChange: (participantId: number, attended: boolean, excusalNote?: string) => void;
  };
}

/** Read-only roster: name, functional role, and RSVP status; optionally an
 * attendance capture control (checkbox + excusal note) per row. */
export function ParticipantList({ participants, attendance }: ParticipantListProps) {
  return (
    <ul className="divide-y divide-brand-slate-100" data-testid="meeting-participant-list">
      {participants.map((p) => {
        const entry = attendance?.entries.get(p.id);
        return (
          <li key={p.id} className="space-y-2 py-2.5" data-testid={`meeting-participant-${p.id}`}>
            <div className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="truncate text-sm font-medium text-brand-slate-800">{p.displayName}</p>
                <p className="text-xs text-brand-slate-400">
                  {TEAM_ROLE_LABELS[p.teamRole]}
                  {p.isRequired ? '' : ' · optional'}
                  {p.isFamily ? ' · family' : ''}
                  {p.isStudent ? ' · student' : ''}
                </p>
              </div>
              <Badge variant={inviteStatusVariant[p.inviteStatus]}>
                {INVITE_STATUS_LABELS[p.inviteStatus]}
              </Badge>
            </div>

            {attendance && (
              <div className="flex flex-wrap items-center gap-3 pl-0.5">
                <label className="flex items-center gap-1.5 text-xs text-brand-slate-600">
                  <input
                    type="checkbox"
                    checked={entry?.attended ?? false}
                    onChange={(e) => attendance.onChange(p.id, e.target.checked, entry?.excusalNote)}
                    className="h-3.5 w-3.5 rounded border-brand-slate-300 text-brand-teal-500 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-400"
                    data-testid={`attendance-checkbox-${p.id}`}
                  />
                  Attended
                </label>
                {!(entry?.attended ?? false) && (
                  <Input
                    id={`excusal-note-${p.id}`}
                    label="Excusal note"
                    value={entry?.excusalNote ?? ''}
                    onChange={(e) => attendance.onChange(p.id, false, e.target.value)}
                    placeholder="Optional"
                    className="w-56"
                  />
                )}
              </div>
            )}
          </li>
        );
      })}
    </ul>
  );
}
