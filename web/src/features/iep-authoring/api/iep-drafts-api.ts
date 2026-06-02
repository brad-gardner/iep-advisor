import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  AccommodationDto,
  CreateIepDraftRequest,
  GoalDto,
  IepDraftDto,
  SectionDto,
  ServiceLineDto,
  TransitionItemDto,
  UpsertAccommodationRequest,
  UpsertGoalRequest,
  UpsertSectionRequest,
  UpsertServiceLineRequest,
  UpsertTransitionItemRequest,
} from '../types';

// ---- Draft-level ----

export async function createDraft(
  studentId: number,
  data: CreateIepDraftRequest
): Promise<ApiResponse<IepDraftDto>> {
  const res = await apiClient.post<ApiResponse<IepDraftDto>>(
    `/api/educator/students/${studentId}/iep-drafts`,
    data
  );
  return res.data;
}

export async function listDrafts(studentId: number): Promise<ApiResponse<IepDraftDto[]>> {
  const res = await apiClient.get<ApiResponse<IepDraftDto[]>>(
    `/api/educator/students/${studentId}/iep-drafts`
  );
  return res.data;
}

export async function getDraft(draftId: number): Promise<ApiResponse<IepDraftDto>> {
  const res = await apiClient.get<ApiResponse<IepDraftDto>>(`/api/iep-drafts/${draftId}`);
  return res.data;
}

// ---- Generic child collection helpers ----

async function createChild<TReq, TRes>(
  draftId: number,
  collection: string,
  body: TReq
): Promise<ApiResponse<TRes>> {
  const res = await apiClient.post<ApiResponse<TRes>>(
    `/api/iep-drafts/${draftId}/${collection}`,
    body
  );
  return res.data;
}

async function updateChild<TReq, TRes>(
  draftId: number,
  collection: string,
  id: number,
  body: TReq
): Promise<ApiResponse<TRes>> {
  const res = await apiClient.put<ApiResponse<TRes>>(
    `/api/iep-drafts/${draftId}/${collection}/${id}`,
    body
  );
  return res.data;
}

async function deleteChild(
  draftId: number,
  collection: string,
  id: number
): Promise<ApiResponse<null>> {
  const res = await apiClient.delete<ApiResponse<null>>(
    `/api/iep-drafts/${draftId}/${collection}/${id}`
  );
  return res.data;
}

// ---- Sections ----
export const createSection = (draftId: number, body: UpsertSectionRequest) =>
  createChild<UpsertSectionRequest, SectionDto>(draftId, 'sections', body);
export const updateSection = (draftId: number, id: number, body: UpsertSectionRequest) =>
  updateChild<UpsertSectionRequest, SectionDto>(draftId, 'sections', id, body);
export const deleteSection = (draftId: number, id: number) =>
  deleteChild(draftId, 'sections', id);

// ---- Goals ----
export const createGoal = (draftId: number, body: UpsertGoalRequest) =>
  createChild<UpsertGoalRequest, GoalDto>(draftId, 'goals', body);
export const updateGoal = (draftId: number, id: number, body: UpsertGoalRequest) =>
  updateChild<UpsertGoalRequest, GoalDto>(draftId, 'goals', id, body);
export const deleteGoal = (draftId: number, id: number) => deleteChild(draftId, 'goals', id);

// ---- Service lines ----
export const createServiceLine = (draftId: number, body: UpsertServiceLineRequest) =>
  createChild<UpsertServiceLineRequest, ServiceLineDto>(draftId, 'service-lines', body);
export const updateServiceLine = (draftId: number, id: number, body: UpsertServiceLineRequest) =>
  updateChild<UpsertServiceLineRequest, ServiceLineDto>(draftId, 'service-lines', id, body);
export const deleteServiceLine = (draftId: number, id: number) =>
  deleteChild(draftId, 'service-lines', id);

// ---- Accommodations ----
export const createAccommodation = (draftId: number, body: UpsertAccommodationRequest) =>
  createChild<UpsertAccommodationRequest, AccommodationDto>(draftId, 'accommodations', body);
export const updateAccommodation = (
  draftId: number,
  id: number,
  body: UpsertAccommodationRequest
) => updateChild<UpsertAccommodationRequest, AccommodationDto>(draftId, 'accommodations', id, body);
export const deleteAccommodation = (draftId: number, id: number) =>
  deleteChild(draftId, 'accommodations', id);

// ---- Transition items ----
export const createTransitionItem = (draftId: number, body: UpsertTransitionItemRequest) =>
  createChild<UpsertTransitionItemRequest, TransitionItemDto>(draftId, 'transition-items', body);
export const updateTransitionItem = (
  draftId: number,
  id: number,
  body: UpsertTransitionItemRequest
) =>
  updateChild<UpsertTransitionItemRequest, TransitionItemDto>(
    draftId,
    'transition-items',
    id,
    body
  );
export const deleteTransitionItem = (draftId: number, id: number) =>
  deleteChild(draftId, 'transition-items', id);
