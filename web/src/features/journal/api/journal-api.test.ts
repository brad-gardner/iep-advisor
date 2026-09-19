import { describe, expect, it, vi, beforeEach } from 'vitest';

const apiClient = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() }));
vi.mock('@/lib/api-client', () => ({ apiClient }));

import { createJournalEntry, deleteJournalEntry, listJournalEntries, updateJournalEntry } from './journal-api';

describe('journal-api', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiClient.get.mockResolvedValue({ data: { success: true, data: [] } });
    apiClient.post.mockResolvedValue({ data: { success: true } });
    apiClient.put.mockResolvedValue({ data: { success: true } });
    apiClient.delete.mockResolvedValue({ data: { success: true } });
  });

  it('lists a child journal with only the query params that are set', async () => {
    await listJournalEntries(4);
    expect(apiClient.get).toHaveBeenLastCalledWith('/api/children/4/journal', { params: {} });
    await listJournalEntries(4, { tag: 'Medical', take: 5 });
    expect(apiClient.get).toHaveBeenLastCalledWith('/api/children/4/journal', { params: { tag: 'Medical', take: 5 } });
  });

  it('creates under the child and updates/deletes by entry id', async () => {
    const body = { occurredOn: '2026-09-12', tag: 'Incident' as const, contentMarkdown: 'x' };
    await createJournalEntry(4, body);
    expect(apiClient.post).toHaveBeenCalledWith('/api/children/4/journal', body);
    await updateJournalEntry(9, body);
    expect(apiClient.put).toHaveBeenCalledWith('/api/journal/9', body);
    await deleteJournalEntry(9);
    expect(apiClient.delete).toHaveBeenCalledWith('/api/journal/9');
  });
});
