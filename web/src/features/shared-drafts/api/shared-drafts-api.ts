import axios from 'axios';
import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  AskQuestionRequest,
  CreateResponseRequest,
  DraftAnswerDto,
  DraftExplanationDto,
  DraftResponseDto,
  MeetingSummaryDto,
  ParentDraftNoteDto,
  SharedDraftRevisionDetailDto,
  SharedDraftRevisionDto,
} from '../types';

// Thin async wrappers over apiClient for the parent shared-draft-review
// endpoints (plan6-contract.md). Error responses reject as an AxiosError whose
// body is the ApiResponse envelope — callers catch with `apiErrorMessage`.

/** Every revision ever shared for this child, all statuses, newest first. */
export async function getSharedDrafts(childId: number): Promise<ApiResponse<SharedDraftRevisionDto[]>> {
  const res = await apiClient.get<ApiResponse<SharedDraftRevisionDto[]>>(
    `/api/children/${childId}/shared-drafts`
  );
  return res.data;
}

/** One frozen revision (values + pinned template). Withdrawn/superseded stay readable. */
export async function getSharedDraft(revisionId: number): Promise<ApiResponse<SharedDraftRevisionDetailDto>> {
  const res = await apiClient.get<ApiResponse<SharedDraftRevisionDetailDto>>(
    `/api/shared-drafts/${revisionId}`
  );
  return res.data;
}

/**
 * Plain-language explanations for this revision — generated on first call via
 * Claude and cached server-side; never regenerated. A 503 means generation
 * failed and there is no partial cache to fall back to.
 */
export async function getDraftExplanations(revisionId: number): Promise<ApiResponse<DraftExplanationDto>> {
  const res = await apiClient.get<ApiResponse<DraftExplanationDto>>(
    `/api/shared-drafts/${revisionId}/explanations`
  );
  return res.data;
}

/** Ask a private question about this revision; persists a ParentDraftNote. */
export async function askDraftQuestion(
  revisionId: number,
  request: AskQuestionRequest
): Promise<ApiResponse<DraftAnswerDto>> {
  const res = await apiClient.post<ApiResponse<DraftAnswerDto>>(
    `/api/shared-drafts/${revisionId}/ask`,
    request
  );
  return res.data;
}

/** This parent's own private Q&A notes on the revision — never visible to staff. */
export async function getDraftNotes(revisionId: number): Promise<ApiResponse<ParentDraftNoteDto[]>> {
  const res = await apiClient.get<ApiResponse<ParentDraftNoteDto[]>>(
    `/api/shared-drafts/${revisionId}/notes`
  );
  return res.data;
}

export async function deleteDraftNote(noteId: number): Promise<ApiResponse<null>> {
  const res = await apiClient.delete<ApiResponse<null>>(`/api/notes/${noteId}`);
  return res.data;
}

/** Submit an Agree/Question/ChangeRequest/Comment. Only valid on an Active revision. */
export async function createDraftResponse(
  revisionId: number,
  request: CreateResponseRequest
): Promise<ApiResponse<DraftResponseDto>> {
  const res = await apiClient.post<ApiResponse<DraftResponseDto>>(
    `/api/shared-drafts/${revisionId}/responses`,
    request
  );
  return res.data;
}

/** This parent's own responses on the revision, including staff replies. */
export async function getDraftResponses(revisionId: number): Promise<ApiResponse<DraftResponseDto[]>> {
  const res = await apiClient.get<ApiResponse<DraftResponseDto[]>>(
    `/api/shared-drafts/${revisionId}/responses`
  );
  return res.data;
}

/** Idempotent "I've reviewed this" stamp — not consent, not a signature. */
export async function acknowledgeSharedDraft(
  revisionId: number
): Promise<ApiResponse<SharedDraftRevisionDto>> {
  const res = await apiClient.post<ApiResponse<SharedDraftRevisionDto>>(
    `/api/shared-drafts/${revisionId}/acknowledge`,
    {}
  );
  return res.data;
}

// ---------------------------------------------------------------------------
// Post-meeting family summary — staff draft/edit/send, family read (Sent only).
// ---------------------------------------------------------------------------

export async function draftMeetingSummary(meetingId: number): Promise<ApiResponse<MeetingSummaryDto>> {
  const res = await apiClient.post<ApiResponse<MeetingSummaryDto>>(
    `/api/meetings/${meetingId}/summary/draft`,
    {}
  );
  return res.data;
}

export async function updateMeetingSummary(
  meetingId: number,
  body: string
): Promise<ApiResponse<MeetingSummaryDto>> {
  const res = await apiClient.put<ApiResponse<MeetingSummaryDto>>(`/api/meetings/${meetingId}/summary`, {
    body,
  });
  return res.data;
}

export async function sendMeetingSummary(meetingId: number): Promise<ApiResponse<MeetingSummaryDto>> {
  const res = await apiClient.post<ApiResponse<MeetingSummaryDto>>(
    `/api/meetings/${meetingId}/summary/send`,
    {}
  );
  return res.data;
}

/** 404 (no summary yet, or a family caller before it's Sent) resolves to `null`. */
export async function getMeetingSummary(meetingId: number): Promise<MeetingSummaryDto | null> {
  try {
    const res = await apiClient.get<ApiResponse<MeetingSummaryDto>>(`/api/meetings/${meetingId}/summary`);
    return res.data.success && res.data.data ? res.data.data : null;
  } catch (err) {
    if (axios.isAxiosError(err) && err.response?.status === 404) return null;
    throw err;
  }
}
