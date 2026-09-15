import { describe, it, expect, vi, beforeEach } from 'vitest';

const apiClient = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() }));
vi.mock('@/lib/api-client', () => ({ apiClient }));

import { removeTeamMember, searchStudents } from './educator-api';

describe('educator-api', () => {
  beforeEach(() => vi.clearAllMocks());

  describe('removeTeamMember', () => {
    it('treats an empty 204 body as success', async () => {
      apiClient.delete.mockResolvedValue({ status: 204, data: '' });
      await expect(removeTeamMember(10, 100)).resolves.toEqual({ success: true, data: null });
      expect(apiClient.delete).toHaveBeenCalledWith('/api/educator/students/10/team/100');
    });

    it('passes a 200 envelope through untouched', async () => {
      const envelope = { success: true, data: null, message: 'Team member removed.' };
      apiClient.delete.mockResolvedValue({ status: 200, data: envelope });
      await expect(removeTeamMember(10, 100)).resolves.toBe(envelope);
    });
  });

  describe('searchStudents', () => {
    it('serializes only the defined params, including the attention filter', async () => {
      apiClient.get.mockResolvedValue({
        status: 200,
        data: { success: true, data: { items: [], total: 0, page: 1, pageSize: 50 } },
      });
      await searchStudents({ attention: 'NoCaseManager', page: 2, pageSize: 50, status: 'Active' });
      expect(apiClient.get).toHaveBeenCalledWith('/api/educator/students', {
        params: { attention: 'NoCaseManager', page: '2', pageSize: '50', status: 'Active' },
      });
    });
  });
});
