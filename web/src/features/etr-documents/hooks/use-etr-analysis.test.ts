import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import {
  makeAnalysisRunLatest,
  makeAnalysisRunSource,
  makeRunSection,
} from "@/features/analysis/test/fixtures";
import type { EtrCompletenessPayload, EtrEligibilityPayload } from "@/features/analysis/types";

const analysisRunsApi = vi.hoisted(() => ({
  getLatestForSource: vi.fn(),
  createRun: vi.fn(),
}));
vi.mock("@/features/analysis/api/analysis-runs-api", () => analysisRunsApi);

import { useEtrAnalysis } from "./use-etr-analysis";

describe("useEtrAnalysis", () => {
  beforeEach(() => vi.clearAllMocks());

  it("normalizes a null list field on the completeness/eligibility payloads to [] instead of passing null through", async () => {
    const source = makeAnalysisRunSource({ id: 501, sourceType: "EtrDocument", sourceId: 55 });
    // Simulates a server response that coalesced list fields to null rather
    // than [] — views downstream call .join/.length/.map on these directly.
    const completeness = {
      evaluatedDomains: null,
      missingDomains: null,
      overallCompletenessRating: "thin",
    } as unknown as EtrCompletenessPayload;
    const eligibility = {
      statedCategory: "SLD",
      statedConclusion: "Eligible",
      dataSupportsConclusion: true,
      supportingEvidence: null,
      contradictingEvidence: null,
      alternativeConsiderations: null,
      notes: null,
    } as unknown as EtrEligibilityPayload;

    const run = makeAnalysisRunLatest({
      sources: [source],
      sections: [
        makeRunSection({
          id: 1,
          analysisRunSourceId: 501,
          sectionKind: "etr_completeness",
          etrCompleteness: completeness,
        }),
        makeRunSection({
          id: 2,
          analysisRunSourceId: 501,
          sectionKind: "etr_eligibility",
          etrEligibility: eligibility,
        }),
      ],
    });
    analysisRunsApi.getLatestForSource.mockResolvedValue({ success: true, data: run });

    const { result } = renderHook(() => useEtrAnalysis(4, 55));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.completeness?.evaluatedDomains).toEqual([]);
    expect(result.current.completeness?.missingDomains).toEqual([]);
    expect(result.current.eligibility?.supportingEvidence).toEqual([]);
    expect(result.current.eligibility?.contradictingEvidence).toEqual([]);
    expect(result.current.eligibility?.alternativeConsiderations).toEqual([]);
  });

  it("normalizes a null toolsUsed on an individual evaluated domain", async () => {
    const source = makeAnalysisRunSource({ id: 501, sourceType: "EtrDocument", sourceId: 55 });
    const completeness = {
      evaluatedDomains: [{ domain: "Cognitive", toolsUsed: null, adequacyRating: "adequate", notes: null }],
      missingDomains: [],
      overallCompletenessRating: "adequate",
    } as unknown as EtrCompletenessPayload;

    const run = makeAnalysisRunLatest({
      sources: [source],
      sections: [
        makeRunSection({
          id: 1,
          analysisRunSourceId: 501,
          sectionKind: "etr_completeness",
          etrCompleteness: completeness,
        }),
      ],
    });
    analysisRunsApi.getLatestForSource.mockResolvedValue({ success: true, data: run });

    const { result } = renderHook(() => useEtrAnalysis(4, 55));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.completeness?.evaluatedDomains[0].toolsUsed).toEqual([]);
  });

  it("treats a 404 from the latest-run endpoint as never analyzed, not an error", async () => {
    analysisRunsApi.getLatestForSource.mockRejectedValue({ response: { status: 404 } });

    const { result } = renderHook(() => useEtrAnalysis(4, 55));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.run).toBeNull();
    expect(result.current.completeness).toBeNull();
    expect(result.current.eligibility).toBeNull();
    expect(result.current.loadError).toBeNull();
  });
});
