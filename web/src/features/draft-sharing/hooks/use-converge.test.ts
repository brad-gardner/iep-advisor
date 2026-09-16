import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ConvergeDto } from '../types';

const draftSharingApi = vi.hoisted(() => ({ getConverge: vi.fn() }));
vi.mock('../api/draft-sharing-api', () => draftSharingApi);

import { useConverge } from './use-converge';

function makeConverge(overrides: Partial<ConvergeDto> = {}): ConvergeDto {
  return {
    instanceId: 7,
    latestRevision: null,
    openResponses: [],
    resolvedResponses: [],
    changesSinceShare: null,
    acknowledgements: [],
    canShare: true,
    policyEnabled: true,
    ...overrides,
  };
}

describe('useConverge', () => {
  beforeEach(() => vi.clearAllMocks());

  it('keeps the loaded data on screen during a refresh so an open resolve dialog survives', async () => {
    const first = makeConverge({ instanceId: 7 });
    draftSharingApi.getConverge.mockResolvedValueOnce({ success: true, data: first });
    const { result } = renderHook(() => useConverge(7));
    await waitFor(() => expect(result.current.converge).toBe(first));

    // The refresh (e.g. after "Share again") is still in flight: no spinner, previous data intact.
    let resolveRefresh!: (value: { success: boolean; data: ConvergeDto }) => void;
    draftSharingApi.getConverge.mockReturnValueOnce(new Promise((resolve) => (resolveRefresh = resolve)));
    act(() => result.current.retry());
    expect(result.current.isLoading).toBe(false);
    expect(result.current.converge).toBe(first);

    const second = makeConverge({ instanceId: 7, acknowledgements: [{ parentName: 'Jamie', acknowledgedAt: '2026-09-10T00:00:00.000Z' }] });
    await act(async () => resolveRefresh({ success: true, data: second }));
    expect(result.current.converge).toBe(second);
  });

  it('drops the previous document when the instance changes', async () => {
    draftSharingApi.getConverge.mockResolvedValueOnce({ success: true, data: makeConverge({ instanceId: 7 }) });
    const { result, rerender } = renderHook(({ id }) => useConverge(id), { initialProps: { id: 7 } });
    await waitFor(() => expect(result.current.converge?.instanceId).toBe(7));

    draftSharingApi.getConverge.mockReturnValueOnce(new Promise(() => {}));
    rerender({ id: 8 });
    expect(result.current.converge).toBeNull();
    expect(result.current.isLoading).toBe(true);
  });
});
