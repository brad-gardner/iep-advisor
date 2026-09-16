import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ExportJobDto } from '../types';

const exportsApi = vi.hoisted(() => ({ listDistrictExports: vi.fn(), enqueueDistrictExport: vi.fn() }));
vi.mock('../api/exports-api', () => exportsApi);

import { useDistrictExports } from './use-district-exports';

function job(id: number, status: ExportJobDto['status']): ExportJobDto {
  return {
    id,
    scope: 'District',
    status,
    requestedAt: '2026-09-16T00:00:00.000Z',
    requestedByName: 'Admin',
    startedAt: null,
    completedAt: null,
    sizeBytes: null,
    studentCount: 0,
    fileCount: 0,
    error: null,
  } as ExportJobDto;
}

describe('useDistrictExports', () => {
  beforeEach(() => vi.clearAllMocks());

  it('lets the latest response win when an older poll resolves late', async () => {
    let resolveSlow!: (v: unknown) => void;
    // First load: slow (it will resolve last with a stale Queued snapshot).
    exportsApi.listDistrictExports.mockReturnValueOnce(new Promise((r) => (resolveSlow = r)));
    const { result } = renderHook(() => useDistrictExports());

    // A manual reload (what a successful request triggers) resolves first with the fresh state.
    exportsApi.listDistrictExports.mockResolvedValueOnce({ success: true, data: [job(1, 'Completed')] });
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.jobs[0]?.status).toBe('Completed'));

    await act(async () => resolveSlow({ success: true, data: [job(1, 'Queued')] }));
    expect(result.current.jobs[0]?.status).toBe('Completed'); // stale snapshot ignored
  });
});
