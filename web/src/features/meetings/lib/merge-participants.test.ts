import { describe, expect, it } from 'vitest';
import { mergeWithMeeting } from './merge-participants';
import type { ParticipantRow } from '../hooks/use-meeting-participant-pool';
import { makeMeeting, makeParticipant } from '../test/fixtures';

const poolRow = (userId: number, displayName: string): ParticipantRow => ({
  key: `team-${userId}`,
  kind: 'team',
  displayName,
  userId,
  teamRole: 'Other',
  isRequired: true,
  checked: true,
});

describe('mergeWithMeeting', () => {
  it('keeps already-invited users as checked rows even when the directory did not return them', () => {
    const meeting = makeMeeting({
      participants: [
        makeParticipant({ id: 1, userId: 7, displayName: 'Casey Manager', teamRole: 'CaseManager', isRequired: true }),
        makeParticipant({ id: 2, userId: 41, displayName: 'Pat Parent', teamRole: 'Other', isFamily: true, isRequired: false }),
        makeParticipant({ id: 3, userId: null, displayName: 'Dr. Ext', externalName: 'Dr. Ext', externalEmail: 'ext@example.com', teamRole: 'Other' }),
      ],
    });
    // Directory outage: only Casey came back; the pool is otherwise empty.
    const rows = mergeWithMeeting([poolRow(7, 'Casey Manager')], meeting);
    expect(rows.map((r) => [r.displayName, r.checked, r.kind, r.userId ?? r.externalEmail])).toEqual([
      ['Casey Manager', true, 'team', 7],
      ['Pat Parent', true, 'family', 41],
      ['Dr. Ext', true, 'external', 'ext@example.com'],
    ]);
    expect(rows[1].isRequired).toBe(false);
  });

  it('unchecks pool rows that are not on the meeting', () => {
    const meeting = makeMeeting({ participants: [makeParticipant({ id: 1, userId: 7 })] });
    const rows = mergeWithMeeting([poolRow(7, 'Casey'), poolRow(8, 'Dana')], meeting);
    expect(rows.find((r) => r.userId === 8)?.checked).toBe(false);
  });
});
