import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { AnalysisRun } from "../types";

const useAnalysisRunHook = vi.hoisted(() => ({ useAnalysisRun: vi.fn() }));
vi.mock("../hooks/use-analysis-run", () => useAnalysisRunHook);

import { RunDetail } from "./run-detail";

function baseRun(overrides: Partial<AnalysisRun> = {}): AnalysisRun {
  return {
    id: 7,
    childProfileId: 4,
    status: "Running",
    overallSummary: null,
    crossDocSynthesis: null,
    overallRedFlags: [],
    advocacyGapAnalysis: null,
    parentGoalsSnapshot: [],
    sources: [],
    sections: [],
    errorMessage: null,
    createdAt: "2026-09-20T00:00:00Z",
    ...overrides,
  };
}

function renderDetail(run: AnalysisRun | null, extra: { isLoading?: boolean; pollTimedOut?: boolean } = {}) {
  useAnalysisRunHook.useAnalysisRun.mockReturnValue({
    run,
    isLoading: extra.isLoading ?? false,
    pollTimedOut: extra.pollTimedOut ?? false,
    reload: vi.fn(),
  });
  return render(
    <MemoryRouter>
      <RunDetail childId={4} runId={7} />
    </MemoryRouter>
  );
}

describe("RunDetail", () => {
  it("shows how many documents have been analyzed while the run is in progress", () => {
    const run = baseRun({
      status: "Running",
      sources: [
        { id: 1, sourceType: "IepDocument", sourceId: 12, sourceLabel: "IEP", status: "Completed", errorMessage: null },
        { id: 2, sourceType: "EtrDocument", sourceId: 55, sourceLabel: "ETR", status: "Error", errorMessage: "failed" },
        { id: 3, sourceType: "ProgressReport", sourceId: 9, sourceLabel: "PR", status: "Running", errorMessage: null },
      ],
    });
    renderDetail(run);

    expect(screen.getByText(/2 of 3 documents analyzed\./)).toBeInTheDocument();
  });

  it("shows an info notice with the run's note when a completed run skipped synthesis", () => {
    const run = baseRun({
      status: "Completed",
      errorMessage: "Synthesis was skipped after a source failed.",
    });
    renderDetail(run);

    expect(screen.getByText("Analysis completed with a note")).toBeInTheDocument();
    expect(screen.getByText("Synthesis was skipped after a source failed.")).toBeInTheDocument();
  });

  it("does not show the N of M progress line once the run is complete", () => {
    const run = baseRun({
      status: "Completed",
      sources: [
        { id: 1, sourceType: "IepDocument", sourceId: 12, sourceLabel: "IEP", status: "Completed", errorMessage: null },
      ],
    });
    renderDetail(run);

    expect(screen.queryByText(/documents analyzed\./)).not.toBeInTheDocument();
  });
});
