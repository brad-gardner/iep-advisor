import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';

const analysisRunsApi = vi.hoisted(() => ({
  getLatestForSource: vi.fn(),
  createRun: vi.fn(),
}));
vi.mock('@/features/analysis/api/analysis-runs-api', () => analysisRunsApi);

import { useIepAnalysis } from './use-iep-analysis';

describe('useIepAnalysis', () => {
  beforeEach(() => vi.clearAllMocks());

  it('treats a 404 from the latest-run endpoint as never analyzed, not an error', async () => {
    analysisRunsApi.getLatestForSource.mockRejectedValue({
      response: { status: 404, data: { success: false, message: 'No analysis found for this document.' } },
    });

    const { result } = renderHook(() => useIepAnalysis(4, 12));
    expect(result.current.isLoading).toBe(true);

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.run).toBeNull();
    expect(result.current.source).toBeNull();
    expect(result.current.goalAnalyses).toBeNull();
    expect(result.current.otherSources).toEqual([]);
    expect(result.current.stale).toBe(false);
  });

  it('exposes this document\'s own source, sections, and goal ratings from the run', async () => {
    const run = {
      id: 7,
      childProfileId: 4,
      status: 'Completed',
      overallSummary: 'Summary',
      crossDocSynthesis: null,
      overallRedFlags: [],
      advocacyGapAnalysis: null,
      parentGoalsSnapshot: [],
      sources: [
        { id: 501, sourceType: 'IepDocument', sourceId: 12, sourceLabel: 'IEP', status: 'Completed', errorMessage: null },
      ],
      sections: [
        {
          id: 1,
          analysisRunSourceId: 501,
          sectionKind: 'iep_goals',
          analysis: null,
          goalAnalyses: [{ goalId: 340 }],
          displayOrder: 0,
        },
      ],
      errorMessage: null,
      createdAt: '2026-03-05T00:00:00Z',
      otherSources: [],
      stale: false,
    };
    analysisRunsApi.getLatestForSource.mockResolvedValue({ success: true, data: run });

    const { result } = renderHook(() => useIepAnalysis(4, 12));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.run).toEqual(run);
    expect(result.current.source?.id).toBe(501);
    expect(result.current.goalAnalyses).toEqual([{ goalId: 340 }]);
  });
});
