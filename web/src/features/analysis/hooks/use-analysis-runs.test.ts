import { describe, expect, it, vi, beforeEach } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";
import { makeAnalysisRun } from "../test/fixtures";

const analysisRunsApi = vi.hoisted(() => ({ listRuns: vi.fn() }));
vi.mock("../api/analysis-runs-api", () => analysisRunsApi);

import { useAnalysisRuns } from "./use-analysis-runs";

describe("useAnalysisRuns", () => {
  beforeEach(() => vi.clearAllMocks());

  it("resets to an empty, loading list immediately when childId changes, instead of showing the previous child's runs", async () => {
    analysisRunsApi.listRuns.mockResolvedValueOnce({
      success: true,
      data: [makeAnalysisRun({ id: 1, childProfileId: 4 })],
    });
    const { result, rerender } = renderHook(({ childId }) => useAnalysisRuns(childId), {
      initialProps: { childId: 4 },
    });
    await waitFor(() => expect(result.current.runs).toHaveLength(1));

    let resolveSecondChild!: (v: unknown) => void;
    analysisRunsApi.listRuns.mockReturnValueOnce(new Promise((r) => (resolveSecondChild = r)));
    rerender({ childId: 5 });

    // The previous child's runs are gone immediately, not left showing stale
    // data while the new child's request is in flight.
    expect(result.current.runs).toEqual([]);
    expect(result.current.isLoading).toBe(true);

    await act(async () => {
      resolveSecondChild({ success: true, data: [makeAnalysisRun({ id: 2, childProfileId: 5 })] });
    });
    expect(result.current.runs[0]?.id).toBe(2);
  });

  it("lets the newest reload win when an older, slower one resolves late (request-generation guard)", async () => {
    // Initial mount load.
    analysisRunsApi.listRuns.mockResolvedValueOnce({ success: true, data: [] });
    const { result } = renderHook(() => useAnalysisRuns(4));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // An older reload is kicked off and is slow to resolve...
    let resolveOlder!: (v: unknown) => void;
    analysisRunsApi.listRuns.mockReturnValueOnce(new Promise((r) => (resolveOlder = r)));
    let olderReload!: Promise<void>;
    act(() => {
      olderReload = result.current.reload();
    });

    // ...a newer reload (e.g. a second trigger in quick succession) fires
    // and resolves first with the fresher state.
    analysisRunsApi.listRuns.mockResolvedValueOnce({
      success: true,
      data: [makeAnalysisRun({ id: 1, status: "Completed" })],
    });
    await act(async () => {
      await result.current.reload();
    });
    expect(result.current.runs[0]?.status).toBe("Completed");

    // The older, slower reload finally resolves — it must not overwrite the
    // fresher state set by the newer one.
    await act(async () => {
      resolveOlder({ success: true, data: [makeAnalysisRun({ id: 1, status: "Running" })] });
      await olderReload;
    });
    expect(result.current.runs[0]?.status).toBe("Completed");
  });

  it("gives a newly started run a fresh polling window instead of carrying over a prior timeout", async () => {
    vi.useFakeTimers();
    try {
      analysisRunsApi.listRuns.mockResolvedValue({
        success: true,
        data: [makeAnalysisRun({ id: 1, status: "Running" })],
      });
      const { result } = renderHook(() => useAnalysisRuns(4));
      await act(async () => {
        await vi.advanceTimersByTimeAsync(0);
      });
      expect(result.current.hasInFlight).toBe(true);
      expect(result.current.pollTimedOut).toBe(false);

      // Run the polling window all the way out so it trips into "still
      // working" (180 polls x 5s = 15 minutes).
      await act(async () => {
        await vi.advanceTimersByTimeAsync(180 * 5000);
      });
      expect(result.current.pollTimedOut).toBe(true);

      // A reload (as happens right after starting a new run) resolves with a
      // still-running run — it must get a fresh polling window rather than
      // inheriting the prior timeout.
      analysisRunsApi.listRuns.mockResolvedValue({
        success: true,
        data: [makeAnalysisRun({ id: 2, status: "Running" })],
      });
      await act(async () => {
        await result.current.reload();
      });
      expect(result.current.pollTimedOut).toBe(false);
      expect(result.current.hasInFlight).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });
});
