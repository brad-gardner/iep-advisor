import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api";
import type {
  AnalysisRun,
  AnalysisRunLatest,
  AnalysisSourceType,
  CreateAnalysisRunRequest,
} from "../types";

export async function listRuns(
  childId: number
): Promise<ApiResponse<AnalysisRun[]>> {
  const response = await apiClient.get<ApiResponse<AnalysisRun[]>>(
    `/api/children/${childId}/analysis-runs`
  );
  return response.data;
}

export async function getRun(
  childId: number,
  runId: number
): Promise<ApiResponse<AnalysisRun>> {
  const response = await apiClient.get<ApiResponse<AnalysisRun>>(
    `/api/children/${childId}/analysis-runs/${runId}`
  );
  return response.data;
}

export async function createRun(
  childId: number,
  payload: CreateAnalysisRunRequest
): Promise<ApiResponse<AnalysisRun>> {
  const response = await apiClient.post<ApiResponse<AnalysisRun>>(
    `/api/children/${childId}/analysis-runs`,
    payload
  );
  return response.data;
}

/**
 * The latest run that includes this document/source — 404 when no run has
 * ever included it (the caller treats that as "never analyzed", not an
 * error). Adds `otherSources` (for a "part of a larger analysis" note) and
 * `stale` (the document moved on since this run) to the run shape.
 */
export async function getLatestForSource(
  childId: number,
  sourceType: AnalysisSourceType,
  sourceId: number
): Promise<ApiResponse<AnalysisRunLatest>> {
  const response = await apiClient.get<ApiResponse<AnalysisRunLatest>>(
    `/api/children/${childId}/analysis-runs/latest`,
    { params: { sourceType, sourceId } }
  );
  return response.data;
}
