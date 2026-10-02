import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Outlet, Route, Routes, useLocation } from "react-router-dom";
import type { ChildProfile } from "@/types/api";
import type { AnalysisRun } from "../types";

const toast = vi.hoisted(() => ({ show: vi.fn() }));
vi.mock("@/components/ui/toast", () => ({ useToast: () => toast }));

const analysisRunsHook = vi.hoisted(() => ({ useAnalysisRuns: vi.fn() }));
vi.mock("../hooks/use-analysis-runs", () => analysisRunsHook);

const analysisRunsApi = vi.hoisted(() => ({ createRun: vi.fn() }));
vi.mock("../api/analysis-runs-api", () => analysisRunsApi);

vi.mock("./source-picker", () => ({ SourcePicker: () => null }));

vi.mock("./run-detail", () => ({
  RunDetail: ({ runId }: { runId: number }) => <div data-testid="run-detail">{runId}</div>,
}));

import { ChildAnalysisTab } from "./child-analysis-tab";

function makeRun(id: number, createdAt: string): AnalysisRun {
  return {
    id,
    childProfileId: 4,
    status: "Completed",
    overallSummary: null,
    crossDocSynthesis: null,
    overallRedFlags: [],
    advocacyGapAnalysis: null,
    parentGoalsSnapshot: [],
    sources: [],
    sections: [],
    errorMessage: null,
    createdAt,
  };
}

const child: ChildProfile = {
  id: 4,
  firstName: "Jordan",
  lastName: "Lee",
  dateOfBirth: null,
  gradeLevel: null,
  disabilityCategory: null,
  schoolDistrict: null,
  role: "owner",
  currentIepDocumentId: null,
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
};

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location">{location.pathname + location.search}</output>;
}

function renderTab(url: string) {
  const ctx = { child, childId: 4, reloadChild: () => Promise.resolve() };
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/children/:childId" element={<Outlet context={ctx} />}>
          <Route
            path="analysis"
            element={
              <>
                <ChildAnalysisTab />
                <LocationProbe />
              </>
            }
          />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

// Latest-first, matching the API's OrderByDescending(CreatedAt).
const runs = [makeRun(2, "2026-09-20T00:00:00Z"), makeRun(1, "2026-09-01T00:00:00Z")];

describe("ChildAnalysisTab — run selection via ?run=", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    analysisRunsHook.useAnalysisRuns.mockReturnValue({
      runs,
      isLoading: false,
      reload: vi.fn(),
      hasInFlight: false,
      pollTimedOut: false,
    });
  });

  it("selects the run named in ?run= instead of the latest", async () => {
    renderTab("/children/4/analysis?run=1");
    expect(await screen.findByTestId("run-detail")).toHaveTextContent("1");
    expect(screen.queryByTestId("analysis-run-not-found")).not.toBeInTheDocument();
  });

  it("falls back to the latest run and shows an info notice when the run id is unknown", async () => {
    renderTab("/children/4/analysis?run=999");
    const notice = await screen.findByTestId("analysis-run-not-found");
    expect(notice).toHaveTextContent("That analysis couldn't be found — showing the latest.");
    expect(notice).toHaveAttribute("role", "status");
    expect(screen.getByTestId("run-detail")).toHaveTextContent("2");
    await waitFor(() => expect(screen.getByTestId("location")).toHaveTextContent("run=2"));
  });

  it("auto-selects the latest run (replacing the URL) when none is requested", async () => {
    renderTab("/children/4/analysis");
    expect(await screen.findByTestId("run-detail")).toHaveTextContent("2");
    expect(screen.queryByTestId("analysis-run-not-found")).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId("location")).toHaveTextContent("run=2"));
  });

  it("does not show the not-found notice when the child has no runs at all (nothing to fall back to)", async () => {
    analysisRunsHook.useAnalysisRuns.mockReturnValue({
      runs: [],
      isLoading: false,
      reload: vi.fn(),
      hasInFlight: false,
      pollTimedOut: false,
    });
    renderTab("/children/4/analysis?run=999");
    await screen.findByText("No analysis selected");
    expect(screen.queryByTestId("analysis-run-not-found")).not.toBeInTheDocument();
  });

  it("shows the still-working notice with role=status when polling has timed out", () => {
    analysisRunsHook.useAnalysisRuns.mockReturnValue({
      runs,
      isLoading: false,
      reload: vi.fn(),
      hasInFlight: true,
      pollTimedOut: true,
    });
    renderTab("/children/4/analysis?run=1");
    const notice = screen.getByText("Still working…").closest('[role="status"]');
    expect(notice).not.toBeNull();
  });
});
