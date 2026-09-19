import { describe, expect, it, vi, beforeEach } from 'vitest';

const apiClient = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() }));
vi.mock('@/lib/api-client', () => ({ apiClient }));

import { createPrepQuestion, deletePrepQuestion, listPrepQuestions, reorderPrepQuestions, updatePrepQuestion } from './prep-questions-api';

describe('prep-questions-api', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiClient.get.mockResolvedValue({ data: { success: true, data: [] } });
    apiClient.post.mockResolvedValue({ data: { success: true } });
    apiClient.put.mockResolvedValue({ data: { success: true } });
    apiClient.delete.mockResolvedValue({ data: { success: true } });
  });

  it('lists and creates under the child', async () => {
    await listPrepQuestions(4);
    expect(apiClient.get).toHaveBeenCalledWith('/api/children/4/prep-questions');
    await createPrepQuestion(4, { text: 'Who collects the data?', source: 'advocate' });
    expect(apiClient.post).toHaveBeenCalledWith('/api/children/4/prep-questions', { text: 'Who collects the data?', source: 'advocate' });
  });

  it('updates and deletes by question id, and saves the order under the child', async () => {
    await updatePrepQuestion(9, { isChecked: true });
    expect(apiClient.put).toHaveBeenLastCalledWith('/api/prep-questions/9', { isChecked: true });
    await reorderPrepQuestions(4, [3, 1, 2]);
    expect(apiClient.put).toHaveBeenLastCalledWith('/api/children/4/prep-questions/order', { ids: [3, 1, 2] });
    await deletePrepQuestion(9);
    expect(apiClient.delete).toHaveBeenCalledWith('/api/prep-questions/9');
  });
});
