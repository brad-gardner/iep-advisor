// TypeScript mirrors of the P5a/P5b backend DTOs
// (api/.../DTOs/IepVersions/IepVersionDtos.cs).

export type PdfRenderStatus = 'Pending' | 'Rendered' | 'Error';

// ---- Requests ----

export interface FinalizeIepDraftRequest {
  effectiveDate?: string | null; // ISO date (yyyy-MM-dd), optional
}

// ---- Responses ----

export interface IepVersionSummaryDto {
  id: number;
  schoolStudentId: number;
  sourceDraftId: number;
  versionNumber: number;
  documentType: string;
  title: string | null;
  effectiveDate: string | null;
  finalizedByUserId: number;
  finalizedAt: string;
  pdfRenderStatus: string | null;
}

export interface IepVersionSectionDto {
  id: number;
  sectionKind: string;
  richText: string | null;
  displayOrder: number;
  lineageId: string;
}

export interface IepVersionGoalDto {
  id: number;
  domain: string | null;
  goalText: string | null;
  baseline: string | null;
  targetCriteria: string | null;
  measurementMethod: string | null;
  timeframe: string | null;
  displayOrder: number;
  lineageId: string;
}

export interface IepVersionServiceLineDto {
  id: number;
  serviceType: string | null;
  frequency: string | null;
  duration: string | null;
  location: string | null;
  providerRole: string | null;
  startDate: string | null;
  endDate: string | null;
  displayOrder: number;
  lineageId: string;
}

export interface IepVersionAccommodationDto {
  id: number;
  category: string | null;
  text: string | null;
  displayOrder: number;
  lineageId: string;
}

export interface IepVersionTransitionItemDto {
  id: number;
  postsecondaryGoalArea: string | null;
  servicesText: string | null;
  displayOrder: number;
  lineageId: string;
}

export interface IepVersionDto {
  id: number;
  schoolStudentId: number;
  sourceDraftId: number;
  versionNumber: number;
  documentType: string;
  title: string | null;
  effectiveDate: string | null;
  finalizedByUserId: number;
  finalizedAt: string;
  pdfRenderStatus: string | null;
  pdfBlobUri: string | null;
  pdfRenderedAt: string | null;
  sections: IepVersionSectionDto[];
  goals: IepVersionGoalDto[];
  serviceLines: IepVersionServiceLineDto[];
  accommodations: IepVersionAccommodationDto[];
  transitionItems: IepVersionTransitionItemDto[];
}

export interface IepVersionPdfStatusDto {
  versionId: number;
  renderStatus: PdfRenderStatus;
  url: string | null;
  renderedAt: string | null;
  errorMessage: string | null;
}
