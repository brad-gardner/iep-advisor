// Mirrors api/IepAssistant.Api/DTOs/ChildLinks/ChildLinkDtos.cs

export interface LinkableChild {
  childProfileId: number;
  firstName: string;
  lastName?: string | null;
}

export interface ChildLinkInvitePreview {
  schoolStudentId: number;
  studentFirstName: string;
  studentLastName?: string | null;
  schoolName?: string | null;
  existingChildren: LinkableChild[];
}

export interface AcceptedChildLink {
  id: number;
  schoolStudentId: number;
  childProfileId?: number | null;
  isAccepted: boolean;
  linkedAt?: string | null;
}

export interface ChildSchoolLink {
  id: number;
  schoolStudentId: number;
  schoolName?: string | null;
  studentFirstName: string;
  studentLastName?: string | null;
  linkedAt?: string | null;
}
