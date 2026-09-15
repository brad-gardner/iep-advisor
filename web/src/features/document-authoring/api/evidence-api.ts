import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';

export type EvidenceKind =
  | 'Identity'
  | 'TeamMember'
  | 'PriorGoal'
  | 'PriorService'
  | 'PriorAccommodation'
  | 'PriorTransition'
  | 'PresentLevels'
  | 'EtrFinding'
  | 'StudentVoice'
  | 'ParentContribution';

export interface EvidenceItem {
  id: string;
  kind: EvidenceKind;
  sourceType: string;
  sourceId: number;
  sourceLabel: string;
  sourceDate: string | null;
  authorRole: 'school' | 'student' | 'family' | 'system';
  text: string;
  rowId: string | null;
  fields: Record<string, string> | null;
}

export interface EvidenceSource {
  sourceType: string;
  sourceId: number;
  label: string;
  date: string | null;
  documentTypeKey: string | null;
}

export interface StudentEvidenceBundle {
  schoolStudentId: number;
  items: EvidenceItem[];
  sources: EvidenceSource[];
}

/** Staff: the role-filtered evidence bundle that grounds prefill and AI assist. */
export async function getStudentEvidence(studentId: number): Promise<ApiResponse<StudentEvidenceBundle>> {
  const res = await apiClient.get<ApiResponse<StudentEvidenceBundle>>(`/api/educator/students/${studentId}/evidence`);
  return res.data;
}
