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
  onboardingCompleted: boolean;
  subscriptionStatus: string;
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
  inviteCode: string;
}

export interface ChildProfile {
  id: number;
  firstName: string;
  lastName: string | null;
  dateOfBirth: string | null;
  gradeLevel: string | null;
  disabilityCategory: string | null;
  schoolDistrict: string | null;
  role: "owner" | "viewer" | "collaborator";
  createdAt: string;
  updatedAt: string;
}

export interface ChildAccessEntry {
  id: number;
  childProfileId: number;
  userId: number | null;
  userEmail: string | null;
  userName: string | null;
  inviteEmail: string | null;
  role: string;
  acceptedAt: string | null;
  isPending: boolean;
  createdAt: string;
}

export interface CreateInviteRequest {
  email: string;
  role: string;
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
  status: "pending" | "analyzing" | "completed" | "error";
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
  alignmentStatus: "addressed" | "partially_addressed" | "not_addressed";
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
  overallRating: "green" | "yellow" | "red";
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
  rating: "green" | "yellow" | "red";
  explanation: string;
}

export interface RedFlag {
  severity: "yellow" | "red";
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

// Meeting Prep types

export interface ChecklistItem {
  text: string;
  context: string | null;
  legalBasis: string | null;
  isChecked: boolean;
}

export interface MeetingPrepChecklist {
  id: number;
  childProfileId: number;
  iepDocumentId: number | null;
  status: "pending" | "generating" | "completed" | "error";
  questionsToAsk: ChecklistItem[];
  redFlagsToRaise: ChecklistItem[];
  preparationNotes: ChecklistItem[];
  // Legacy fields — present on older checklists
  documentsToBring: ChecklistItem[];
  rightsToReference: ChecklistItem[];
  goalGaps: ChecklistItem[];
  generalTips: ChecklistItem[];
  errorMessage: string | null;
  createdAt: string;
}

export interface CheckItemRequest {
  section: string;
  index: number;
  isChecked: boolean;
}

// IEP Comparison types

export interface TimelineEntry {
  id: number;
  iepDate: string;
  meetingType: string | null;
  status: string;
  goalCount: number;
  sectionCount: number;
  redFlagCount: number;
  hasAnalysis: boolean;
}

export interface TimelineResult {
  childId: number;
  ieps: TimelineEntry[];
}

export interface GoalDiff {
  goalText: string;
  domain: string | null;
}

export interface GoalChangeDetail {
  field: string;
  older: string | null;
  newer: string | null;
}

export interface ModifiedGoalDiff {
  domain: string | null;
  olderGoalText: string;
  newerGoalText: string;
  changes: GoalChangeDetail[];
}

export interface GoalChanges {
  added: GoalDiff[];
  removed: GoalDiff[];
  modified: ModifiedGoalDiff[];
}

export interface SectionChanges {
  added: string[];
  removed: string[];
  inBoth: string[];
}

export interface RedFlagChange {
  title: string;
  wasInOlder: boolean;
  addressedInNewer: boolean;
}

export interface RedFlagResolution {
  title: string;
  wasInOlder: boolean;
}

export interface ComparisonSummary {
  goalsAdded: number;
  goalsRemoved: number;
  goalsModified: number;
  goalsUnchanged: number;
  sectionsAdded: number;
  sectionsRemoved: number;
  redFlagsResolved: number;
  redFlagsPersisting: number;
  newRedFlags: number;
}

export interface RedFlagResolutionResult {
  resolved: RedFlagChange[];
  persisting: RedFlagChange[];
  newFlags: RedFlagResolution[];
}

export interface ComparisonResult {
  olderIepId: number;
  newerIepId: number;
  olderDate: string;
  newerDate: string;
  goalChanges: GoalChanges;
  sectionChanges: SectionChanges;
  redFlagResolution: RedFlagResolutionResult;
  summary: ComparisonSummary;
}

// Knowledge Base types

export interface KnowledgeBaseEntry {
  id: number;
  title: string;
  content: string;
  category: string;
  legalReference: string | null;
  state: string | null;
  tags: string[];
}

export interface CategoryCount {
  category: string;
  count: number;
}

// Admin types

export interface AdminUser {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  state: string | null;
  role: string;
  isActive: boolean;
  createdAt: string;
}

export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  state?: string;
  role?: string;
  isActive?: boolean;
}

export interface AdminDashboardStats {
  totalUsers: number;
  activeUsers: number;
  adminUsers: number;
  usersWithSubscription: number;
  usersOnboarded: number;
  totalChildren: number;
  totalDocuments: number;
  documentsParsed: number;
  documentsCreated: number;
  documentsError: number;
  totalAnalyses: number;
  analysesCompleted: number;
  analysesError: number;
  totalGoals: number;
  totalChecklists: number;
  checklistsCompleted: number;
  totalAnalysisUsage: number;
  totalMeetingPrepUsage: number;
  totalBetaCodes: number;
  redeemedBetaCodes: number;
  totalSharedAccess: number;
  newUsersLast7Days: number;
  newDocumentsLast7Days: number;
  analysesLast7Days: number;
  documentsByMeetingType: Record<string, number>;
  goalsByCategory: Record<string, number>;
  childrenByDisabilityCategory: Record<string, number>;
  usersBySubscriptionStatus: Record<string, number>;
}

// Subscription types

export interface SubscriptionStatus {
  status: string; // none, active, past_due, canceled, expired
  expiresAt: string | null;
  childUsage: Record<string, ChildUsage>;
}

export interface ChildUsage {
  childId: number;
  childName: string;
  analysisCount: number;
  analysisLimit: number;
}
