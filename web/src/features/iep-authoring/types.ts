// TypeScript mirrors of the P4a backend DTOs (api/.../IepDrafts/IepDraftDtos.cs).

export type IepSectionKind =
  | 'StudentProfile'
  | 'PresentLevels'
  | 'Eligibility'
  | 'Placement'
  | 'ProgressMonitoring'
  | 'SpecialFactors'
  | 'Other';

// ---- Child DTOs ----

export interface SectionDto {
  id: number;
  iepDraftId: number;
  sectionKind: string;
  richText: string | null;
  displayOrder: number;
  lineageId: string;
  lastEditedByUserId: number | null;
  lastEditedAt: string | null;
}

export interface GoalDto {
  id: number;
  iepDraftId: number;
  domain: string | null;
  goalText: string | null;
  baseline: string | null;
  targetCriteria: string | null;
  measurementMethod: string | null;
  timeframe: string | null;
  displayOrder: number;
  lineageId: string;
  lastEditedByUserId: number | null;
  lastEditedAt: string | null;
}

export interface ServiceLineDto {
  id: number;
  iepDraftId: number;
  serviceType: string | null;
  frequency: string | null;
  duration: string | null;
  location: string | null;
  providerRole: string | null;
  startDate: string | null;
  endDate: string | null;
  displayOrder: number;
  lineageId: string;
  lastEditedByUserId: number | null;
  lastEditedAt: string | null;
}

export interface AccommodationDto {
  id: number;
  iepDraftId: number;
  category: string | null;
  text: string | null;
  displayOrder: number;
  lineageId: string;
  lastEditedByUserId: number | null;
  lastEditedAt: string | null;
}

export interface TransitionItemDto {
  id: number;
  iepDraftId: number;
  postsecondaryGoalArea: string | null;
  servicesText: string | null;
  displayOrder: number;
  lineageId: string;
  lastEditedByUserId: number | null;
  lastEditedAt: string | null;
}

export interface IepDraftDto {
  id: number;
  schoolStudentId: number;
  status: string;
  documentType: string;
  title: string | null;
  lastEditedByUserId: number | null;
  lastEditedAt: string | null;
  createdAt: string;
  sections: SectionDto[];
  goals: GoalDto[];
  serviceLines: ServiceLineDto[];
  accommodations: AccommodationDto[];
  transitionItems: TransitionItemDto[];
}

// ---- Upsert requests (all optional except section kind) ----

export interface CreateIepDraftRequest {
  title?: string;
}

export interface UpsertSectionRequest {
  sectionKind: IepSectionKind;
  richText?: string | null;
}

export interface UpsertGoalRequest {
  domain?: string | null;
  goalText?: string | null;
  baseline?: string | null;
  targetCriteria?: string | null;
  measurementMethod?: string | null;
  timeframe?: string | null;
}

export interface UpsertServiceLineRequest {
  serviceType?: string | null;
  frequency?: string | null;
  duration?: string | null;
  location?: string | null;
  providerRole?: string | null;
  startDate?: string | null;
  endDate?: string | null;
}

export interface UpsertAccommodationRequest {
  category?: string | null;
  text?: string | null;
}

export interface UpsertTransitionItemRequest {
  postsecondaryGoalArea?: string | null;
  servicesText?: string | null;
}
