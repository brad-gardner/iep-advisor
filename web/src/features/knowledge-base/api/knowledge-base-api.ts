import { apiClient } from '@/lib/api-client';
import type { ApiResponse, KnowledgeBaseEntry, CategoryCount } from '@/types/api';

export async function searchKnowledgeBase(
  query?: string,
  category?: string | null,
  state?: string | null
): Promise<KnowledgeBaseEntry[]> {
  const params = new URLSearchParams();
  if (query) params.set('query', query);
  if (category) params.set('category', category);
  if (state) params.set('state', state);

  const { data } = await apiClient.get<ApiResponse<KnowledgeBaseEntry[]>>(
    `/api/knowledge-base?${params.toString()}`
  );
  return data.data ?? [];
}

export async function getKnowledgeBaseEntry(id: number): Promise<KnowledgeBaseEntry> {
  const { data } = await apiClient.get<ApiResponse<KnowledgeBaseEntry>>(
    `/api/knowledge-base/${id}`
  );
  return data.data!;
}

export async function getCategories(): Promise<CategoryCount[]> {
  const { data } = await apiClient.get<ApiResponse<CategoryCount[]>>(
    '/api/knowledge-base/categories'
  );
  return data.data ?? [];
}
