export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
  errors?: string[];
}

export interface User {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  state: string | null;
  role: string;
  fullName: string;
  mfaEnabled?: boolean;
}

export interface UpdateProfileRequest {
  firstName?: string;
  lastName?: string;
  state?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token?: string;
  expiresAt?: string;
  user?: User;
  requiresMfa?: boolean;
  mfaPendingToken?: string;
}

// MFA types
export interface MfaSetupResponse {
  otpauthUri: string;
  manualEntryKey: string;
}

export interface MfaVerifySetupResponse {
  recoveryCodes: string[];
}

// Password reset types
export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
}

// Account types
export interface DeleteAccountRequest {
  password: string;
  mfaCode?: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface ChildProfile {
  id: number;
  firstName: string;
  lastName: string | null;
  dateOfBirth: string | null;
  gradeLevel: string | null;
  disabilityCategory: string | null;
  schoolDistrict: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateChildProfileRequest {
  firstName: string;
  lastName?: string;
  dateOfBirth?: string;
  gradeLevel?: string;
  disabilityCategory?: string;
  schoolDistrict?: string;
}

export interface IepDocument {
  id: number;
  childProfileId: number;
  fileName: string;
  uploadDate: string;
  iepDate: string | null;
  status: string;
  fileSizeBytes: number;
  meetingType: string | null;
  attendees: string | null;
  notes: string | null;
  createdAt: string;
}

export interface CreateIepRequest {
  iepDate: string;
  meetingType: string;
  attendees?: string;
  notes?: string;
}

export interface UpdateIepMetadataRequest {
  iepDate?: string;
  meetingType?: string;
  attendees?: string;
  notes?: string;
}

export interface IepSection {
  id: number;
  sectionType: string;
  rawText: string | null;
  parsedContent: string | null;
  displayOrder: number;
  goals: IepGoal[];
}

export interface IepGoal {
  id: number;
  goalText: string;
  domain: string | null;
  baseline: string | null;
  targetCriteria: string | null;
  measurementMethod: string | null;
  timeframe: string | null;
}

export interface UpdateChildProfileRequest {
  firstName?: string;
  lastName?: string;
  dateOfBirth?: string;
  gradeLevel?: string;
  disabilityCategory?: string;
  schoolDistrict?: string;
}

// Advocacy Goals types

export interface AdvocacyGoal {
  id: number;
  childProfileId: number;
  goalText: string;
  category: string | null;
  displayOrder: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateAdvocacyGoalRequest {
  goalText: string;
  category?: string;
}

export interface UpdateAdvocacyGoalRequest {
  goalText?: string;
  category?: string;
}

export interface ReorderAdvocacyGoalsRequest {
  items: { id: number; displayOrder: number }[];
}

// IEP Analysis types

export interface IepAnalysis {
  id: number;
  iepDocumentId: number;
  status: 'pending' | 'analyzing' | 'completed' | 'error';
  overallSummary: string | null;
  sectionAnalyses: SectionAnalysis[];
  goalAnalyses: GoalAnalysis[];
  overallRedFlags: RedFlag[];
  suggestedQuestions: SuggestedQuestion[];
  advocacyGapAnalysis: AdvocacyGapAnalysis | null;
  parentGoalsSnapshot: ParentGoalSnapshot[] | null;
  errorMessage: string | null;
  createdAt: string;
}

export interface AdvocacyGapAnalysis {
  summary: string;
  goalAlignments: GoalAlignment[];
}

export interface GoalAlignment {
  parentGoalText: string;
  parentGoalCategory: string | null;
  alignmentStatus: 'addressed' | 'partially_addressed' | 'not_addressed';
  alignedIepGoals: string[];
  explanation: string;
  recommendation: string | null;
}

export interface ParentGoalSnapshot {
  id: number;
  goalText: string;
  category: string | null;
  displayOrder: number;
}

export interface SectionAnalysis {
  sectionType: string;
  plainLanguageSummary: string;
  keyPoints: string[];
  redFlags: RedFlag[];
  suggestedQuestions: string[];
  legalReferences: LegalReference[];
}

export interface GoalAnalysis {
  goalId: number;
  goalText: string;
  domain: string | null;
  smartAnalysis: SmartAnalysis;
  overallRating: 'green' | 'yellow' | 'red';
  plainLanguageSummary: string;
  strengths: string[];
  concerns: string[];
  suggestedImprovements: string[];
}

export interface SmartAnalysis {
  specific: SmartCriterion;
  measurable: SmartCriterion;
  achievable: SmartCriterion;
  relevant: SmartCriterion;
  timeBound: SmartCriterion;
}

export interface SmartCriterion {
  rating: 'green' | 'yellow' | 'red';
  explanation: string;
}

export interface RedFlag {
  severity: 'yellow' | 'red';
  title: string;
  description: string;
  legalBasis: string | null;
}

export interface SuggestedQuestion {
  question: string;
  context: string;
  category: string;
}

export interface LegalReference {
  provision: string;
  summary: string;
}
