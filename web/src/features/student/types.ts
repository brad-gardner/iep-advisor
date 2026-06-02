// Mirrors api/IepAssistant.Api/DTOs/StudentInvites/*.cs (P7a)

export type StudentInviteSource = 'Parent' | 'Educator';

export interface StudentInviteDto {
  id: number;
  studentEmail: string;
  inviteSource: StudentInviteSource;
  inviteExpiresAt?: string | null;
}

export interface StudentInvitePreviewDto {
  inviteSource: StudentInviteSource;
  linkedToFirstName: string;
  schoolName?: string | null;
  inviteExpiresAt?: string | null;
}

export interface AcceptedStudentInviteDto {
  studentProfileId: number;
  childProfileId?: number | null;
  schoolStudentId?: number | null;
  consentAcceptedAt?: string | null;
}
