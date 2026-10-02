import { describe, expect, it, vi, beforeEach } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";
import { makeAnalysisRun } from "../test/fixtures";

const analysisRunsApi = vi.hoisted(() => ({ getRun: vi.fn() }));
vi.mock("../api/analysis-runs-api", () => analysisRunsApi);

import { useAnalysisRun } from "./use-analysis-run";

describe("useAnalysisRun", () => {
  beforeEach(() => vi.clearAllMocks());

  it("drops a stale response for a run the caller has since moved away from (latest-wins)", async () => {
    let resolveFirst!: (v: unknown) => void;
    // First render requests run 1, which resolves slowly.
    analysisRunsApi.getRun.mockReturnValueOnce(new Promise((r) => (resolveFirst = r)));

    const { result, rerender } = renderHook(({ runId }) => useAnalysisRun(4, runId), {
      initialProps: { runId: 1 },
    });

    // The caller switches to run 2 before run 1's response arrives — this
    // resolves fast with the fresh run.
    analysisRunsApi.getRun.mockResolvedValueOnce({
      success: true,
      data: makeAnalysisRun({ id: 2, status: "Completed" }),
    });
    rerender({ runId: 2 });
    await waitFor(() => expect(result.current.run?.id).toBe(2));

    // Run 1's slow response finally arrives — it must not clobber run 2.
    await act(async () => {
      resolveFirst({ success: true, data: makeAnalysisRun({ id: 1, status: "Running" }) });
    });

    expect(result.current.run?.id).toBe(2);
  });
});
