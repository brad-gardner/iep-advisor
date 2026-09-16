import { useEffect, useState } from 'react';
import { getEligibleTeamStaff } from '@/features/educator/api/educator-api';
import type { TeamRole } from '@/features/educator/types';
import type { ApiResponse } from '@/types/api';
import { getDefaultParticipants } from '../api/meetings-api';

export type ParticipantRowKind = 'team' | 'eligible' | 'family' | 'student' | 'external';

export interface ParticipantRow {
  key: string;
  kind: ParticipantRowKind;
  displayName: string;
  userId?: number;
  externalName?: string;
  externalEmail?: string;
  teamRole: TeamRole;
  isRequired: boolean;
  checked: boolean;
}

interface UseMeetingParticipantPoolResult {
  /** `null` while loading. */
  rows: ParticipantRow[] | null;
  failed: boolean;
}

/** Unwraps a settled directory call to its data array (`[]` on any failure —
 * a rejected promise or a graceful `{ success: false }`) plus whether it
 * actually succeeded. Each of the two directory calls below is independently
 * useful, so one's failure must not discard the other's real data. */
function settledData<T>(result: PromiseSettledResult<ApiResponse<T[]>>): { data: T[]; ok: boolean } {
  if (result.status === 'fulfilled' && result.value.success && result.value.data) {
    return { data: result.value.data, ok: true };
  }
  return { data: [], ok: false };
}

/**
 * Candidate participants for the schedule form: the server's own defaults
 * (active IEP team, accepted family members, the student's account — all
 * real users, pre-checked) plus staff eligible to join the team but not yet
 * on it (unchecked add-ons). Sending these back explicitly keeps the roster
 * identical to what the server would build when participants are omitted.
 */
export function useMeetingParticipantPool(studentId: number): UseMeetingParticipantPoolResult {
  const [rows, setRows] = useState<ParticipantRow[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let active = true;
    (async () => {
      // `allSettled`, not `all`: the two directory calls are independent, so
      // one failing (e.g. a transient error fetching eligible staff) must
      // still let the other's real data seed `rows` and unblock manual
      // participant entry, rather than leaving `rows` `null` forever.
      const [defaultsResult, eligibleResult] = await Promise.allSettled([
        getDefaultParticipants(studentId),
        getEligibleTeamStaff(studentId),
      ]);
      if (!active) return;

      const defaultsOutcome = settledData(defaultsResult);
      const eligibleOutcome = settledData(eligibleResult);

      const defaults = defaultsOutcome.data;
      const defaultRows: ParticipantRow[] = defaults.map((d) => ({
        key: `${d.isStudent ? 'student' : d.isFamily ? 'family' : 'team'}-${d.userId}`,
        kind: d.isStudent ? ('student' as const) : d.isFamily ? ('family' as const) : ('team' as const),
        displayName: d.displayName,
        userId: d.userId,
        teamRole: d.teamRole,
        isRequired: true,
        checked: true,
      }));
      const known = new Set(defaults.map((d) => d.userId));

      const eligibleRows: ParticipantRow[] = eligibleOutcome.data
        .filter((s) => !known.has(s.userId))
        .map((s) => ({
          key: `eligible-${s.staffProfileId}`,
          kind: 'eligible' as const,
          displayName: `${s.firstName} ${s.lastName}`.trim(),
          userId: s.userId,
          teamRole: 'Other' as TeamRole,
          isRequired: true,
          checked: false,
        }));

      setRows([...defaultRows, ...eligibleRows]);
      if (!defaultsOutcome.ok || !eligibleOutcome.ok) setFailed(true);
    })();
    return () => {
      active = false;
    };
  }, [studentId]);

  return { rows, failed };
}
