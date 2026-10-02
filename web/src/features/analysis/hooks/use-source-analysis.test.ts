import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";
import {
  makeAnalysisRunLatest,
  makeAnalysisRunSource,
  makeRunSection,
} from "../test/fixtures";

const analysisRunsApi = vi.hoisted(() => ({
  getLatestForSource: vi.fn(),
  createRun: vi.fn(),
}));
vi.mock("../api/analysis-runs-api", () => analysisRunsApi);

import { useSourceAnalysis } from "./use-source-analysis";

describe("useSourceAnalysis", () => {
  beforeEach(() => vi.clearAllMocks());

  it("treats a 404 from the latest-run endpoint as never analyzed, not an error", async () => {
    analysisRunsApi.getLatestForSource.mockRejectedValue({
      response: { status: 404, data: { message: "No analysis found for this document." } },
    });

    const { result } = renderHook(() => useSourceAnalysis(4, "IepDocument", 12));
    expect(result.current.isLoading).toBe(true);

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.run).toBeNull();
    expect(result.current.loadError).toBeNull();
    expect(result.current.source).toBeNull();
  });

  it("exposes this source's own entry, ordinary sections, and a section looked up by kind", async () => {
    const source = makeAnalysisRunSource({ id: 501, sourceType: "IepDocument", sourceId: 12 });
    const ordinarySection = makeRunSection({
      id: 1,
      analysisRunSourceId: 501,
      sectionKind: "present_levels",
      analysis: {
        sectionKind: "present_levels",
        plainLanguageSummary: "Fine.",
        keyPoints: [],
        redFlags: [],
        legalReferences: [],
      },
    });
    const goalsSection = makeRunSection({
      id: 2,
      analysisRunSourceId: 501,
      sectionKind: "iep_goals",
      goalAnalyses: [{ goalId: 340 } as never],
    });
    const run = makeAnalysisRunLatest({
      sources: [source],
      sections: [ordinarySection, goalsSection],
    });
    analysisRunsApi.getLatestForSource.mockResolvedValue({ success: true, data: run });

    const { result } = renderHook(() => useSourceAnalysis(4, "IepDocument", 12));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.source?.id).toBe(501);
    expect(result.current.sections).toEqual([ordinarySection]);
    expect(result.current.sectionOfKind("iep_goals")).toEqual(goalsSection);
  });

  it("keeps the previous run and surfaces loadError on a non-404 failure, instead of clearing the run", async () => {
    const run = makeAnalysisRunLatest({ sources: [makeAnalysisRunSource()] });
    analysisRunsApi.getLatestForSource.mockResolvedValueOnce({ success: true, data: run });

    const { result } = renderHook(() => useSourceAnalysis(4, "IepDocument", 12));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.run).toEqual(run);

    analysisRunsApi.getLatestForSource.mockRejectedValueOnce({
      response: { status: 500, data: { message: "Server hiccup." } },
    });
    await act(async () => {
      await result.current.reload();
    });

    expect(result.current.loadError).toBe("Server hiccup.");
    expect(result.current.run).toEqual(run);
  });

  it("seeds the triggered run immediately so a failed follow-up load doesn't drop it", async () => {
    analysisRunsApi.getLatestForSource.mockRejectedValue({ response: { status: 404 } });
    const { result } = renderHook(() => useSourceAnalysis(4, "IepDocument", 12));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.run).toBeNull();

    const created = {
      id: 99,
      childProfileId: 4,
      status: "Pending" as const,
      overallSummary: null,
      crossDocSynthesis: null,
      overallRedFlags: [],
      advocacyGapAnalysis: null,
      parentGoalsSnapshot: [],
      sources: [makeAnalysisRunSource({ id: 601, sourceId: 12, status: "Pending" })],
      sections: [],
      errorMessage: null,
      createdAt: "2026-03-06T00:00:00Z",
    };
    analysisRunsApi.createRun.mockResolvedValue({ success: true, data: created });
    // The follow-up load() inside trigger() fails — the seeded run must survive.
    analysisRunsApi.getLatestForSource.mockRejectedValue({
      response: { status: 500, data: { message: "Still down." } },
    });

    await act(async () => {
      await result.current.trigger();
    });

    expect(result.current.run?.id).toBe(99);
    expect(result.current.run?.status).toBe("Pending");
    expect(result.current.source?.id).toBe(601);
  });

  it("resets to a loading state immediately when sourceId changes, and drops an out-of-order response for the old sourceId", async () => {
    const oldRun = makeAnalysisRunLatest({
      id: 1,
      sources: [makeAnalysisRunSource({ id: 501, sourceId: 12 })],
    });
    analysisRunsApi.getLatestForSource.mockResolvedValueOnce({ success: true, data: oldRun });

    const { result, rerender } = renderHook(
      ({ sourceId }: { sourceId: number }) => useSourceAnalysis(4, "IepDocument", sourceId),
      { initialProps: { sourceId: 12 } }
    );
    await waitFor(() => expect(result.current.run?.id).toBe(1));

    // A reload for sourceId 12 is kicked off and is slow to resolve...
    let resolveOldSourceRequest!: (v: unknown) => void;
    analysisRunsApi.getLatestForSource.mockReturnValueOnce(
      new Promise((resolve) => {
        resolveOldSourceRequest = resolve;
      })
    );
    act(() => {
      result.current.reload();
    });

    // ...and the source changes before it resolves. The hook resets
    // immediately rather than showing sourceId 12's run under sourceId 13.
    let resolveNewSourceRequest!: (v: unknown) => void;
    analysisRunsApi.getLatestForSource.mockReturnValueOnce(
      new Promise((resolve) => {
        resolveNewSourceRequest = resolve;
      })
    );
    rerender({ sourceId: 13 });

    expect(result.current.run).toBeNull();
    expect(result.current.isLoading).toBe(true);

    const newRun = makeAnalysisRunLatest({
      id: 2,
      sources: [makeAnalysisRunSource({ id: 601, sourceId: 13 })],
    });
    await act(async () => {
      resolveNewSourceRequest({ success: true, data: newRun });
    });
    expect(result.current.run?.id).toBe(2);

    // The stale response for sourceId 12 finally resolves — it must not
    // overwrite sourceId 13's run.
    await act(async () => {
      resolveOldSourceRequest({ success: true, data: oldRun });
    });
    expect(result.current.run?.id).toBe(2);
  });

  it("doesn't let a stale, slower reload overwrite a trigger's freshly seeded run", async () => {
    analysisRunsApi.getLatestForSource.mockRejectedValueOnce({ response: { status: 404 } });
    const { result } = renderHook(() => useSourceAnalysis(4, "IepDocument", 12));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // A reload is kicked off and is slow to resolve...
    let resolveStaleReload!: (v: unknown) => void;
    analysisRunsApi.getLatestForSource.mockReturnValueOnce(
      new Promise((resolve) => {
        resolveStaleReload = resolve;
      })
    );
    let staleReload!: Promise<void>;
    act(() => {
      staleReload = result.current.reload();
    });

    // ...a trigger starts a new run and seeds it immediately.
    const created = {
      id: 99,
      childProfileId: 4,
      status: "Pending" as const,
      overallSummary: null,
      crossDocSynthesis: null,
      overallRedFlags: [],
      advocacyGapAnalysis: null,
      parentGoalsSnapshot: [],
      sources: [makeAnalysisRunSource({ id: 601, sourceId: 12, status: "Pending" })],
      sections: [],
      errorMessage: null,
      createdAt: "2026-03-06T00:00:00Z",
    };
    analysisRunsApi.createRun.mockResolvedValue({ success: true, data: created });
    // The follow-up load() inside trigger() resolves with the same seeded run.
    analysisRunsApi.getLatestForSource.mockResolvedValueOnce({
      success: true,
      data: { ...created, otherSources: [], stale: false },
    });

    await act(async () => {
      await result.current.trigger();
    });
    expect(result.current.run?.id).toBe(99);

    // The older, slower reload finally resolves with a different run — it
    // must not clobber the trigger's seeded run.
    const staleRun = makeAnalysisRunLatest({
      id: 1,
      sources: [makeAnalysisRunSource({ id: 501, sourceId: 12 })],
    });
    await act(async () => {
      resolveStaleReload({ success: true, data: staleRun });
      await staleReload;
    });
    expect(result.current.run?.id).toBe(99);
  });
});
