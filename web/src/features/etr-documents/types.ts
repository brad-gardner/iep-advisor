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

export interface EtrDocumentListItem extends EtrDocument {
  childId: number;
  childFirstName: string;
  childLastName: string;
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

export type EtrAnalysisStatus = 'pending' | 'analyzing' | 'completed' | 'error';

export interface EtrAnalysis {
  id: number;
  etrDocumentId: number;
  status: EtrAnalysisStatus;
  assessmentCompleteness: AssessmentCompleteness | null;
  eligibilityReview: EligibilityReview | null;
  overallRedFlags: EtrRedFlag[];
  suggestedQuestions: EtrSuggestedQuestion[];
  overallSummary: string | null;
  errorMessage: string | null;
  createdAt: string;
}

export type AdequacyRating = 'strong' | 'adequate' | 'thin' | 'concerning' | string;

export interface EvaluatedDomain {
  domain: string;
  tools_used?: string | string[];
  adequacy_rating: AdequacyRating;
  notes?: string;
}

export interface MissingDomain {
  domain: string;
  rationale: string;
}

export interface AssessmentCompleteness {
  evaluated_domains: EvaluatedDomain[];
  missing_domains: MissingDomain[];
  overall_completeness_rating: AdequacyRating;
}

export interface EligibilityReview {
  stated_category: string;
  stated_conclusion: string;
  data_supports_conclusion: boolean;
  supporting_evidence: string[];
  contradicting_evidence: string[];
  alternative_considerations: string[];
  notes?: string;
}

export type RedFlagSeverity = 'high' | 'medium' | 'low' | string;

export interface EtrRedFlag {
  severity: RedFlagSeverity;
  category: string;
  finding: string;
  why_it_matters: string;
  parent_right_implicated?: string;
}

export type SuggestedQuestionCategory =
  | 'clarification'
  | 'challenge_eligibility'
  | 'iee_request'
  | 'procedural'
  | 'services_next_steps'
  | string;

export interface EtrSuggestedQuestion {
  category: SuggestedQuestionCategory;
  question: string;
  rationale: string;
}
