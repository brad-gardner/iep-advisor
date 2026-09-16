// Mirrors api/IepAssistant.Api/DTOs/Meetings/*.cs (see plan4-contract.md).
import type { TeamRole } from '@/features/educator/types';

// ---------------------------------------------------------------- Enums
// Enums serialize as their string names (global JsonStringEnumConverter). The
// *_LABELS maps are for DISPLAY ONLY — never compare against labels.

export const MEETING_TYPES = [
  'AnnualReview',
  'InitialIep',
  'Amendment',
  'EtrEligibility',
  'Reevaluation',
  'Transition',
  'ManifestationDetermination',
  'Other',
] as const;
export type MeetingType = (typeof MEETING_TYPES)[number];
export const MEETING_TYPE_LABELS: Record<MeetingType, string> = {
  AnnualReview: 'Annual review',
  InitialIep: 'Initial IEP',
  Amendment: 'Amendment',
  EtrEligibility: 'ETR / eligibility',
  Reevaluation: 'Reevaluation',
  Transition: 'Transition',
  ManifestationDetermination: 'Manifestation determination',
  Other: 'Other',
};

export const MEETING_STATUSES = ['Proposed', 'Scheduled', 'Held', 'Continued', 'Cancelled'] as const;
export type MeetingStatus = (typeof MEETING_STATUSES)[number];
export const MEETING_STATUS_LABELS: Record<MeetingStatus, string> = {
  Proposed: 'Proposed',
  Scheduled: 'Scheduled',
  Held: 'Held',
  Continued: 'Continued',
  Cancelled: 'Cancelled',
};

// The subset of statuses settable via POST /api/meetings/{id}/status.
export const SETTABLE_MEETING_STATUSES = ['Scheduled', 'Held', 'Continued'] as const;
export type SettableMeetingStatus = (typeof SETTABLE_MEETING_STATUSES)[number];

export const INVITE_STATUSES = ['Pending', 'Accepted', 'Declined', 'Tentative'] as const;
export type InviteStatus = (typeof INVITE_STATUSES)[number];
export const INVITE_STATUS_LABELS: Record<InviteStatus, string> = {
  Pending: 'Pending',
  Accepted: 'Accepted',
  Declined: 'Declined',
  Tentative: 'Tentative',
};

export const MIN_MEETING_DURATION_MINUTES = 15;
export const MAX_MEETING_DURATION_MINUTES = 480;
export const DEFAULT_MEETING_DURATION_MINUTES = 60;

// ---------------------------------------------------------------- DTOs

export interface MeetingParticipantDto {
  id: number;
  userId: number | null;
  externalName: string | null;
  externalEmail: string | null;
  displayName: string;
  teamRole: TeamRole;
  isRequired: boolean;
  inviteStatus: InviteStatus;
  attended: boolean | null;
  excusedAt: string | null;
  excusalNote: string | null;
  isFamily: boolean;
  isStudent: boolean;
}

export interface MeetingDto {
  id: number;
  schoolStudentId: number;
  studentName: string;
  type: MeetingType;
  title: string;
  startsAtUtc: string;
  timeZoneId: string;
  durationMinutes: number;
  location: string | null;
  videoUrl: string | null;
  status: MeetingStatus;
  documentInstanceId: number | null;
  notes: string | null;
  sequence: number;
  createdByUserId: number;
  createdAt: string;
  updatedAt: string;
  participants: MeetingParticipantDto[];
  myInviteStatus: InviteStatus | null;
  canManage: boolean;
}

export interface ParticipantInput {
  userId?: number;
  externalName?: string;
  externalEmail?: string;
  teamRole: TeamRole;
  isRequired?: boolean;
}

// Omitting `participants` lets the server compute the default (active team +
// accepted family links + the student's own account); the web client sends an
// explicit array whenever the scheduler customizes the invite list. See
// `components/schedule-meeting-form.tsx` for how the two are reconciled.
export interface CreateMeetingRequest {
  type: MeetingType;
  title?: string;
  startsAtUtc: string;
  timeZoneId?: string;
  durationMinutes?: number;
  location?: string;
  videoUrl?: string;
  documentInstanceId?: number;
  notes?: string;
  participants?: ParticipantInput[];
}

// All fields optional; `participants` is a full replacement when present.
export interface UpdateMeetingRequest {
  type?: MeetingType;
  title?: string;
  startsAtUtc?: string;
  timeZoneId?: string;
  durationMinutes?: number;
  location?: string;
  videoUrl?: string;
  documentInstanceId?: number;
  notes?: string;
  participants?: ParticipantInput[];
}

export interface CancelMeetingRequest {
  reason?: string;
}

export interface SetMeetingStatusRequest {
  status: SettableMeetingStatus;
}

export interface RsvpRequest {
  status: InviteStatus;
}

export interface AttendanceEntry {
  participantId: number;
  attended: boolean;
  excusalNote?: string;
}

export interface AttendanceRequest {
  attendance: AttendanceEntry[];
}

export interface TokenRsvpResult {
  meeting: MeetingDto;
  status: InviteStatus;
}

export interface TokenRsvpRequest {
  token: string;
  status: InviteStatus;
}

/** A suggested participant for a new meeting — mirrors DefaultParticipantDto. */
export interface DefaultParticipantDto {
  userId: number;
  displayName: string;
  email: string;
  teamRole: TeamRole;
  isFamily: boolean;
  isStudent: boolean;
}
