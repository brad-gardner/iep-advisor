import { useMemo } from "react";
import { Card } from "@/components/ui/card";
import { Notice } from "@/components/ui/notice";
import { RedFlagCard } from "@/features/iep-documents/components/red-flag-card";
import { AdvocacyGapAnalysisSection } from "@/features/iep-documents/components/advocacy-gap-analysis";
import { useAnalysisRun } from "../hooks/use-analysis-run";
import { RunStatusBadge } from "./run-status-badge";
import { RunSourceSections } from "./run-source-sections";
import { CrossDocSynthesisSection } from "./cross-doc-synthesis-section";
import type { AnalysisRunSection } from "../types";

interface RunDetailProps {
  childId: number;
  runId: number;
}

export function RunDetail({ childId, runId }: RunDetailProps) {
  const { run, isLoading, pollTimedOut } = useAnalysisRun(childId, runId);

  const sectionsBySource = useMemo(() => {
    const map = new Map<number, AnalysisRunSection[]>();
    if (!run) return map;
    for (const section of run.sections) {
      if (section.analysisRunSourceId === null) continue;
      const list = map.get(section.analysisRunSourceId) ?? [];
      list.push(section);
      map.set(section.analysisRunSourceId, list);
    }
    return map;
  }, [run]);

  if (isLoading && !run) {
    return (
      <Card>
        <p className="text-sm text-brand-slate-400">Loading analysis…</p>
      </Card>
    );
  }

  if (!run) {
    return (
      <Card>
        <p className="text-sm text-brand-slate-400">Select an analysis to view.</p>
      </Card>
    );
  }

  const isComplete = run.status === "Completed";
  const isError = run.status === "Error";
  const isInFlight = run.status === "Pending" || run.status === "Running";

  return (
    <div className="space-y-6" data-testid="analysis-run-detail">
      <div className="flex items-center gap-3">
        <h2 className="font-serif">Analysis</h2>
        <RunStatusBadge status={run.status} />
      </div>

      {isError && (
        <Notice variant="error" title="Analysis failed">
          {run.errorMessage ?? "Something went wrong while running this analysis."}
        </Notice>
      )}

      {isInFlight && pollTimedOut && (
        <Notice variant="info" title="Still working…">
          This analysis is taking longer than usual. Check back shortly.
        </Notice>
      )}

      {isInFlight && !pollTimedOut && (
        <Notice variant="info" title="Analysis in progress">
          We're analyzing your documents. This page updates automatically.
        </Notice>
      )}

      {isComplete && (
        <>
          {run.overallSummary && (
            <Card>
              <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
                Summary
              </h2>
              <p className="text-sm text-brand-slate-600 leading-relaxed">
                {run.overallSummary}
              </p>
            </Card>
          )}

          {run.sources.map((source) => (
            <RunSourceSections
              key={source.id}
              source={source}
              sections={sectionsBySource.get(source.id) ?? []}
            />
          ))}

          {run.crossDocSynthesis && (
            <Card>
              <CrossDocSynthesisSection synthesis={run.crossDocSynthesis} />
            </Card>
          )}

          {run.overallRedFlags.length > 0 && (
            <Card>
              <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-4">
                Overall Concerns
              </h2>
              <div className="space-y-2">
                {run.overallRedFlags.map((flag, i) => (
                  <RedFlagCard key={i} redFlag={flag} />
                ))}
              </div>
            </Card>
          )}

          {run.advocacyGapAnalysis && (
            <Card>
              <AdvocacyGapAnalysisSection
                gapAnalysis={run.advocacyGapAnalysis}
              />
            </Card>
          )}
        </>
      )}
    </div>
  );
}
