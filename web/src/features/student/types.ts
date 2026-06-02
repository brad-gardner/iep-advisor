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

// Mirrors api/IepAssistant.Api/DTOs/StudentWorkspace/*.cs (P8a)

export type StudentWorkspaceEntryKind =
  | 'Strength'
  | 'Interest'
  | 'AccommodationRequest'
  | 'MeetingStatement'
  | 'AiInterviewAnswer';

export interface StudentWorkspaceEntryDto {
  id: number;
  entryKind: StudentWorkspaceEntryKind;
  content: string;
  isShareable: boolean;
  displayOrder: number;
  createdAt: string;
  updatedAt: string;
}

export interface StudentWorkspaceDto {
  id: number;
  userId: number;
  entries: StudentWorkspaceEntryDto[];
}

export interface CreateWorkspaceEntryRequest {
  entryKind: StudentWorkspaceEntryKind;
  content: string;
  isShareable: boolean;
}

export interface UpdateWorkspaceEntryRequest {
  content: string;
  isShareable: boolean;
}

export interface InterviewSuggestionDto {
  suggestion: string;
}
