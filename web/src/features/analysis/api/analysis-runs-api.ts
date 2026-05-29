import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api";
import type { AnalysisRun, CreateAnalysisRunRequest } from "../types";

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
