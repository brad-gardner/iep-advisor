import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { GoalAnalysis, SmartCriterion } from "@/types/api";
import { RunSourceSections } from "./run-source-sections";
import type { AnalysisRunSection, AnalysisRunSource } from "../types";

const ok: SmartCriterion = { rating: "green", explanation: "fine" };

function makeGoal(goalId: number): GoalAnalysis {
  return {
    goalId,
    goalText: "Reading goal text",
    domain: "Reading",
    smartAnalysis: { specific: ok, measurable: ok, achievable: ok, relevant: ok, timeBound: ok },
    overallRating: "green",
    plainLanguageSummary: "Looks fine.",
    strengths: [],
    concerns: [],
    suggestedImprovements: [],
  };
}

function makeSource(overrides: Partial<AnalysisRunSource> = {}): AnalysisRunSource {
  return {
    id: 1,
    sourceType: "IepDocument",
    sourceId: 12,
    sourceLabel: "IEP Mar 2026",
    status: "Completed",
    errorMessage: null,
    ...overrides,
  };
}

function renderSource(source: AnalysisRunSource, sections: AnalysisRunSection[], childId = 4) {
  return render(
    <MemoryRouter>
      <RunSourceSections childId={childId} source={source} sections={sections} />
    </MemoryRouter>
  );
}

describe("RunSourceSections", () => {
  it("renders goal ratings for an IEP source with an iep_goals section", () => {
    const sections: AnalysisRunSection[] = [
      {
        id: 10,
        analysisRunSourceId: 1,
        sectionKind: "iep_goals",
        analysis: null,
        goalAnalyses: [makeGoal(340), makeGoal(341)],
        displayOrder: 0,
      },
    ];
    renderSource(makeSource(), sections);

    expect(screen.getByText("Goal Analysis (2 goals)")).toBeInTheDocument();
    expect(screen.getByTestId("analysis-goal-340")).toBeInTheDocument();
    expect(screen.getByTestId("analysis-goal-341")).toBeInTheDocument();
  });

  it("shows a warning notice instead of sections for a failed source", () => {
    const source = makeSource({ status: "Error", errorMessage: "The document could not be parsed." });
    renderSource(source, []);

    expect(
      screen.getByText("Couldn't analyze this document — start a new analysis to try again.")
    ).toBeInTheDocument();
    expect(screen.getByText("The document could not be parsed.")).toBeInTheDocument();
    expect(screen.queryByText("Goal Analysis")).not.toBeInTheDocument();
  });

  it("shows the warning notice for a failed source even with no error message", () => {
    const source = makeSource({ status: "Error", errorMessage: null });
    renderSource(source, []);

    expect(
      screen.getByText("Couldn't analyze this document — start a new analysis to try again.")
    ).toBeInTheDocument();
  });

  it("links an IEP source header to its document page", () => {
    renderSource(makeSource({ sourceType: "IepDocument", sourceId: 12, sourceLabel: "IEP Mar 2026" }), []);
    const link = screen.getByRole("link", { name: "IEP Mar 2026" });
    expect(link).toHaveAttribute("href", "/children/4/ieps/12");
  });

  it("links an ETR source header to its document page", () => {
    renderSource(
      makeSource({ sourceType: "EtrDocument", sourceId: 55, sourceLabel: "ETR Sep 2025" }),
      []
    );
    const link = screen.getByRole("link", { name: "ETR Sep 2025" });
    expect(link).toHaveAttribute("href", "/children/4/etrs/55");
  });

  it("renders a progress report source as a plain label, not a guessed link", () => {
    renderSource(
      makeSource({ sourceType: "ProgressReport", sourceId: 9, sourceLabel: "Progress Report Jan 2026" }),
      []
    );
    expect(screen.getByText("Progress Report Jan 2026")).toBeInTheDocument();
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });

  it("shows an inline status badge while a source is still running, without a failure notice", () => {
    renderSource(makeSource({ status: "Running" }), []);
    expect(screen.getByText("Running")).toBeInTheDocument();
    expect(screen.queryByText(/Couldn't analyze/)).not.toBeInTheDocument();
  });
});
