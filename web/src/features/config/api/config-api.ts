import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api";

export type FeatureFlagMap = Record<string, boolean>;

export async function getConfig(): Promise<ApiResponse<FeatureFlagMap>> {
  const response = await apiClient.get<ApiResponse<FeatureFlagMap>>(
    "/api/config"
  );
  return response.data;
}
