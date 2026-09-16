import { useEffect, useState } from 'react';
import { getEligibleTeamStaff } from '@/features/educator/api/educator-api';
import type { TeamRole } from '@/features/educator/types';
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
      try {
        const [defaultsRes, eligibleRes] = await Promise.all([
          getDefaultParticipants(studentId),
          getEligibleTeamStaff(studentId),
        ]);
        if (!active) return;

        const defaults = defaultsRes.success && defaultsRes.data ? defaultsRes.data : [];
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

        const eligibleRows: ParticipantRow[] = (
          eligibleRes.success && eligibleRes.data ? eligibleRes.data : []
        )
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
      } catch {
        if (active) setFailed(true);
      }
    })();
    return () => {
      active = false;
    };
  }, [studentId]);

  return { rows, failed };
}
