import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { MeetingBriefDto } from '../types';

const briefApi = vi.hoisted(() => ({ getBrief: vi.fn(), generateBrief: vi.fn() }));
vi.mock('../api/meeting-brief-api', () => briefApi);

import { useMeetingBrief } from './use-meeting-brief';

function brief(meetingId: number, summary: string): MeetingBriefDto {
  return {
    meetingId,
    generatedAt: '2026-09-16T00:00:00.000Z',
    source: { kind: 'Draft', id: 1, label: 'Draft' },
    summary,
    changes: null,
    resourceCommitments: [],
    checklist: [],
    openFamilyResponses: [],
    offlineInput: [],
    contactAttempts: [],
    disclaimer: 'Advisory',
  } as MeetingBriefDto;
}

describe('useMeetingBrief', () => {
  beforeEach(() => vi.clearAllMocks());

  it('drops a regenerate result that resolves after the hook moved to another meeting', async () => {
    briefApi.getBrief.mockImplementation(async (id: number) => ({ success: true, data: brief(id, `Brief for ${id}`) }));
    let finishRegenerate!: (v: unknown) => void;
    briefApi.generateBrief.mockReturnValueOnce(new Promise((r) => (finishRegenerate = r)));

    const { result, rerender } = renderHook(({ id }) => useMeetingBrief(id), { initialProps: { id: 1 } });
    await waitFor(() => expect(result.current.brief?.summary).toBe('Brief for 1'));

    act(() => void result.current.regenerate());
    rerender({ id: 2 });
    await waitFor(() => expect(result.current.brief?.summary).toBe('Brief for 2'));

    await act(async () => finishRegenerate({ success: true, data: brief(1, 'Regenerated 1') }));
    expect(result.current.brief?.summary).toBe('Brief for 2'); // meeting 1's late result never overwrote meeting 2
    expect(result.current.isGenerating).toBe(false); // and meeting 2 did not inherit a stuck spinner
    expect(result.current.generateError).toBeNull();
  });

  it('clears a stale generate error when switching meetings', async () => {
    briefApi.getBrief.mockImplementation(async (id: number) => ({ success: true, data: brief(id, `Brief for ${id}`) }));
    briefApi.generateBrief.mockResolvedValueOnce({ success: false, message: 'Model unavailable' });
    const { result, rerender } = renderHook(({ id }) => useMeetingBrief(id), { initialProps: { id: 1 } });
    await waitFor(() => expect(result.current.brief?.summary).toBe('Brief for 1'));
    await act(async () => result.current.regenerate());
    expect(result.current.generateError).toBe('Model unavailable');

    rerender({ id: 2 });
    expect(result.current.generateError).toBeNull();
    expect(result.current.isGenerating).toBe(false);
  });
});
