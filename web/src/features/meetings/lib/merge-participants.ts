import type { ParticipantRow } from '../hooks/use-meeting-participant-pool';
import type { MeetingDto, ParticipantInput } from '../types';

/**
 * Reconciles the candidate pool (team/eligible/family) with an existing
 * meeting's actual participants: on edit, checked state reflects who is
 * really invited, not the fresh defaults, and anyone on the meeting who isn't
 * in the pool — a one-off external guest, or a user the directory did not
 * return (partial/failed directory load) — becomes its own checked row, so a
 * reschedule can never silently drop someone who is already invited.
 */
export function mergeWithMeeting(pool: ParticipantRow[], meeting?: MeetingDto): ParticipantRow[] {
  if (!meeting) return pool;

  const byUserId = new Map(
    meeting.participants.filter((p) => p.userId != null).map((p) => [p.userId, p])
  );
  const byEmail = new Map(
    meeting.participants.filter((p) => p.externalEmail).map((p) => [p.externalEmail, p])
  );

  const matchedIds = new Set<number>();
  const merged = pool.map((row) => {
    const match =
      row.userId != null
        ? byUserId.get(row.userId)
        : row.externalEmail
          ? byEmail.get(row.externalEmail)
          : undefined;
    if (!match) return { ...row, checked: false };
    matchedIds.add(match.id);
    return { ...row, checked: true, isRequired: match.isRequired, teamRole: match.teamRole };
  });

  const extra: ParticipantRow[] = meeting.participants
    .filter((p) => !matchedIds.has(p.id))
    .map((p) =>
      p.userId != null
        ? {
            key: `existing-${p.id}`,
            kind: p.isStudent ? ('student' as const) : p.isFamily ? ('family' as const) : ('team' as const),
            displayName: p.displayName,
            userId: p.userId,
            teamRole: p.teamRole,
            isRequired: p.isRequired,
            checked: true,
          }
        : {
            key: `existing-${p.id}`,
            kind: 'external' as const,
            displayName: p.displayName,
            externalName: p.externalName ?? undefined,
            externalEmail: p.externalEmail ?? undefined,
            teamRole: p.teamRole,
            isRequired: p.isRequired,
            checked: true,
          }
    );

  return [...merged, ...extra];
}

export function rowToParticipantInput(row: ParticipantRow): ParticipantInput {
  return row.userId != null
    ? { userId: row.userId, teamRole: row.teamRole, isRequired: row.isRequired }
    : {
        externalName: row.externalName,
        externalEmail: row.externalEmail,
        teamRole: row.teamRole,
        isRequired: row.isRequired,
      };
}
