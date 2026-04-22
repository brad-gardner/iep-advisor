export type EvaluationType = 'initial' | 'reevaluation' | 'transfer' | 'other';
export type DocumentState = 'draft' | 'final';
export type EtrStatus = 'created' | 'uploaded' | 'processing' | 'parsed' | 'error';

export interface EtrDocument {
  id: number;
  childProfileId: number;
  fileName: string | null;
  uploadDate: string;
  evaluationDate: string | null;
  evaluationType: string | null;
  documentState: string;
  notes: string | null;
  status: string;
  fileSizeBytes: number;
  createdAt: string;
}

export interface CreateEtrRequest {
  evaluationDate: string;
  evaluationType: EvaluationType;
  documentState: DocumentState;
  notes?: string;
}

export interface UpdateEtrMetadataRequest {
  evaluationDate?: string;
  evaluationType?: EvaluationType;
  documentState?: DocumentState;
  notes?: string;
}

export const EVALUATION_TYPE_LABELS: Record<string, string> = {
  initial: 'Initial Evaluation',
  reevaluation: 'Reevaluation',
  transfer: 'Transfer',
  other: 'Other',
};

export const DOCUMENT_STATE_LABELS: Record<string, string> = {
  draft: 'Draft',
  final: 'Final',
};

export type EtrSectionType =
  | 'referral_reason'
  | 'background_information'
  | 'parent_input'
  | 'teacher_input'
  | 'student_input'
  | 'health_vision_hearing'
  | 'cognitive_assessment'
  | 'academic_assessment'
  | 'behavioral_social_emotional'
  | 'speech_language'
  | 'occupational_physical_therapy'
  | 'adaptive_functional'
  | 'eligibility_determination'
  | 'other';

export interface EtrSection {
  id: number;
  etrDocumentId: number;
  sectionType: string;
  rawText: string | null;
  parsedContent: string | null;
  displayOrder: number;
}
