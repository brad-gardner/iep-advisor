import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { renderInSpanish, resetTestLanguage } from "@/test/i18n-test-utils";
import type { AnalysisRun } from "../types";

const useAnalysisRunHook = vi.hoisted(() => ({ useAnalysisRun: vi.fn() }));
vi.mock("../hooks/use-analysis-run", () => useAnalysisRunHook);

import { RunDetail } from "./run-detail";

function baseRun(overrides: Partial<AnalysisRun> = {}): AnalysisRun {
  return {
    id: 7,
    childProfileId: 4,
    status: "Completed",
    overallSummary: "Resumen de prueba",
    crossDocSynthesis: null,
    overallRedFlags: [],
    advocacyGapAnalysis: null,
    parentGoalsSnapshot: [],
    sources: [],
    sections: [],
    errorMessage: null,
    createdAt: "2026-09-20T00:00:00Z",
    generatedLanguage: "es",
    ...overrides,
  };
}

function renderDetail(run: AnalysisRun | null) {
  useAnalysisRunHook.useAnalysisRun.mockReturnValue({
    run,
    isLoading: false,
    pollTimedOut: false,
    reload: vi.fn(),
  });
  return renderInSpanish(
    <MemoryRouter>
      <RunDetail childId={4} runId={7} />
    </MemoryRouter>,
  );
}

describe("RunDetail in Spanish", () => {
  afterEach(() => resetTestLanguage());

  it("renders the heading and summary heading in Spanish, with no generated-language notice for a Spanish run", async () => {
    await renderDetail(baseRun());

    expect(await screen.findByRole("heading", { name: "Análisis" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Resumen" })).toBeInTheDocument();
    expect(screen.queryByTestId("generated-language-notice")).not.toBeInTheDocument();
  });

  it("shows the generated-in-English notice to a Spanish viewer when the run was generated in English", async () => {
    await renderDetail(baseRun({ generatedLanguage: "en" }));

    const notice = await screen.findByTestId("generated-language-notice");
    expect(notice).toHaveTextContent("Generado en inglés");
  });
});
