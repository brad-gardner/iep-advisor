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
