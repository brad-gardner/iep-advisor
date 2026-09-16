import type { MeetingDto, MeetingParticipantDto } from '../types';

export function makeParticipant(
  overrides: Partial<MeetingParticipantDto> = {}
): MeetingParticipantDto {
  return {
    id: 1,
    userId: 7,
    externalName: null,
    externalEmail: null,
    displayName: 'Casey Manager',
    teamRole: 'CaseManager',
    isRequired: true,
    inviteStatus: 'Pending',
    attended: null,
    excusedAt: null,
    excusalNote: null,
    isFamily: false,
    isStudent: false,
    ...overrides,
  };
}

export function makeMeeting(overrides: Partial<MeetingDto> = {}): MeetingDto {
  return {
    id: 100,
    schoolStudentId: 10,
    studentName: 'Ada Lovelace',
    type: 'AnnualReview',
    title: 'Annual review meeting',
    startsAtUtc: '2026-10-01T15:00:00.000Z',
    timeZoneId: 'America/New_York',
    durationMinutes: 60,
    location: 'Room 204',
    videoUrl: null,
    status: 'Scheduled',
    documentInstanceId: null,
    notes: null,
    sequence: 0,
    createdByUserId: 1,
    createdAt: '2026-09-01T12:00:00.000Z',
    updatedAt: '2026-09-01T12:00:00.000Z',
    participants: [makeParticipant()],
    myInviteStatus: null,
    canManage: true,
    ...overrides,
  };
}
