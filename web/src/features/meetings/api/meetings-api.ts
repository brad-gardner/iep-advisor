import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  AttendanceRequest,
  CancelMeetingRequest,
  CreateMeetingRequest,
  DefaultParticipantDto,
  MeetingDto,
  RsvpRequest,
  SetMeetingStatusRequest,
  TokenRsvpRequest,
  TokenRsvpResult,
  UpdateMeetingRequest,
} from '../types';

const STUDENTS = '/api/educator/students';
const MEETINGS = '/api/meetings';

export async function createMeeting(
  studentId: number,
  data: CreateMeetingRequest
): Promise<ApiResponse<MeetingDto>> {
  const response = await apiClient.post<ApiResponse<MeetingDto>>(
    `${STUDENTS}/${studentId}/meetings`,
    data
  );
  return response.data;
}

/** The participants a new meeting gets by default: active team, accepted family, student account. */
export async function getDefaultParticipants(
  studentId: number
): Promise<ApiResponse<DefaultParticipantDto[]>> {
  const response = await apiClient.get<ApiResponse<DefaultParticipantDto[]>>(
    `${STUDENTS}/${studentId}/meetings/default-participants`
  );
  return response.data;
}

export async function listStudentMeetings(studentId: number): Promise<ApiResponse<MeetingDto[]>> {
  const response = await apiClient.get<ApiResponse<MeetingDto[]>>(
    `${STUDENTS}/${studentId}/meetings`
  );
  return response.data;
}

export async function getMeeting(meetingId: number): Promise<ApiResponse<MeetingDto>> {
  const response = await apiClient.get<ApiResponse<MeetingDto>>(`${MEETINGS}/${meetingId}`);
  return response.data;
}

export async function updateMeeting(
  meetingId: number,
  data: UpdateMeetingRequest
): Promise<ApiResponse<MeetingDto>> {
  const response = await apiClient.put<ApiResponse<MeetingDto>>(`${MEETINGS}/${meetingId}`, data);
  return response.data;
}

export async function cancelMeeting(
  meetingId: number,
  data: CancelMeetingRequest = {}
): Promise<ApiResponse<MeetingDto>> {
  const response = await apiClient.post<ApiResponse<MeetingDto>>(
    `${MEETINGS}/${meetingId}/cancel`,
    data
  );
  return response.data;
}

export async function setMeetingStatus(
  meetingId: number,
  data: SetMeetingStatusRequest
): Promise<ApiResponse<MeetingDto>> {
  const response = await apiClient.post<ApiResponse<MeetingDto>>(
    `${MEETINGS}/${meetingId}/status`,
    data
  );
  return response.data;
}

export async function recordAttendance(
  meetingId: number,
  data: AttendanceRequest
): Promise<ApiResponse<MeetingDto>> {
  const response = await apiClient.post<ApiResponse<MeetingDto>>(
    `${MEETINGS}/${meetingId}/attendance`,
    data
  );
  return response.data;
}

export async function rsvpToMeeting(
  meetingId: number,
  data: RsvpRequest
): Promise<ApiResponse<MeetingDto>> {
  const response = await apiClient.post<ApiResponse<MeetingDto>>(
    `${MEETINGS}/${meetingId}/rsvp`,
    data
  );
  return response.data;
}

export async function listMyMeetings(
  params: { from?: string; to?: string } = {}
): Promise<ApiResponse<MeetingDto[]>> {
  const query: Record<string, string> = {};
  if (params.from) query.from = params.from;
  if (params.to) query.to = params.to;
  const response = await apiClient.get<ApiResponse<MeetingDto[]>>(`${MEETINGS}/mine`, {
    params: query,
  });
  return response.data;
}

export async function listChildMeetings(childId: number): Promise<ApiResponse<MeetingDto[]>> {
  const response = await apiClient.get<ApiResponse<MeetingDto[]>>(
    `/api/children/${childId}/meetings`
  );
  return response.data;
}

// Token RSVP (email link, no login).
export async function getMeetingByToken(token: string): Promise<ApiResponse<TokenRsvpResult>> {
  const response = await apiClient.get<ApiResponse<TokenRsvpResult>>(`${MEETINGS}/rsvp`, {
    params: { token },
  });
  return response.data;
}

export async function submitTokenRsvp(
  data: TokenRsvpRequest
): Promise<ApiResponse<TokenRsvpResult>> {
  const response = await apiClient.post<ApiResponse<TokenRsvpResult>>(`${MEETINGS}/rsvp`, data);
  return response.data;
}

// A real navigation target (anonymous auth still required by the API — this
// just builds the href), not a fetch — used by "Copy ICS link" and by any
// `<a href>`/download affordance.
export function meetingIcsUrl(meetingId: number): string {
  return `${MEETINGS}/${meetingId}.ics`;
}
