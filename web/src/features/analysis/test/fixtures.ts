import type {
  AnalysisRun,
  AnalysisRunLatest,
  AnalysisRunSection,
  AnalysisRunSource,
} from "../types";

/**
 * Shared factory for `AnalysisRunSection` test fixtures. `AnalysisRunSection`
 * grew `etrCompleteness`/`etrEligibility` alongside `goalAnalyses`, so every
 * inline section literal across the test suite needs all three (even when
 * null) to satisfy the type — build from this instead of a fresh literal.
 */
export function makeRunSection(overrides: Partial<AnalysisRunSection> = {}): AnalysisRunSection {
  return {
    id: 1,
    analysisRunSourceId: null,
    sectionKind: "present_levels",
    analysis: null,
    goalAnalyses: null,
    etrCompleteness: null,
    etrEligibility: null,
    displayOrder: 0,
    ...overrides,
  };
}

export function makeAnalysisRunSource(overrides: Partial<AnalysisRunSource> = {}): AnalysisRunSource {
  return {
    id: 501,
    sourceType: "IepDocument",
    sourceId: 12,
    sourceLabel: "Document",
    status: "Completed",
    errorMessage: null,
    ...overrides,
  };
}

export function makeAnalysisRun(overrides: Partial<AnalysisRun> = {}): AnalysisRun {
  return {
    id: 7,
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
    createdAt: "2026-03-05T00:00:00Z",
    ...overrides,
  };
}

export function makeAnalysisRunLatest(overrides: Partial<AnalysisRunLatest> = {}): AnalysisRunLatest {
  return {
    ...makeAnalysisRun(),
    otherSources: [],
    stale: false,
    ...overrides,
  };
}
