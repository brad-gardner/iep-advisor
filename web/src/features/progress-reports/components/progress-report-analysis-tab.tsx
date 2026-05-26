import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Notice } from "@/components/ui/notice";
import { Badge } from "@/components/ui/badge";
import { AdvocacyGapAnalysisSection } from "@/features/iep-documents/components/advocacy-gap-analysis";
import { useProgressReportAnalysis } from "../hooks/use-progress-report-analysis";
import { GoalProgressCard } from "./goal-progress-card";

interface ProgressReportAnalysisTabProps {
  progressReportId: number;
}

export function ProgressReportAnalysisTab({
  progressReportId,
}: ProgressReportAnalysisTabProps) {
  const { analysis, status, loading, isTriggering, error, start } =
    useProgressReportAnalysis(progressReportId);

  if (loading && !analysis) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (error && !analysis) {
    return (
      <Notice variant="error" title="Couldn't load analysis">
        {error}
      </Notice>
    );
  }

  if (status === "none") {
    return (
      <Card className="text-center py-12">
        <h3 className="font-serif text-[20px] font-semibold text-brand-slate-800 mb-2">
          Analyze this progress report
        </h3>
        <p className="text-sm text-brand-slate-500 mb-4 max-w-md mx-auto">
          We'll review the report against this IEP's goals and any advocacy
          goals you've set. You'll get per-goal progress findings, red flags,
          and concrete questions to bring to the next meeting.
        </p>
        <Button onClick={start} disabled={isTriggering}>
          {isTriggering ? "Starting..." : "Run Analysis"}
        </Button>
      </Card>
    );
  }

  if (status === "pending" || status === "analyzing") {
    return (
      <Card className="text-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500 mx-auto mb-4" />
        <p className="text-sm text-brand-slate-500">
          Analyzing the progress report. This usually takes 1–3 minutes.
        </p>
      </Card>
    );
  }

  if (status === "error") {
    return (
      <Card className="text-center py-12">
        <Notice variant="error" title="Analysis failed">
          {analysis?.errorMessage ||
            "An error occurred while analyzing this report."}
        </Notice>
        <div className="mt-4">
          <Button onClick={start} disabled={isTriggering}>
            {isTriggering ? "Retrying..." : "Retry Analysis"}
          </Button>
        </div>
      </Card>
    );
  }

  if (!analysis) return null;

  return (
    <div className="space-y-6" data-testid="progress-report-analysis">
      {analysis.summary && (
        <Card>
          <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
            Summary
          </h2>
          <p className="text-sm text-brand-slate-600 whitespace-pre-wrap">
            {analysis.summary}
          </p>
        </Card>
      )}

      {analysis.goalProgressFindings.length > 0 && (
        <section>
          <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-3">
            Goal Progress ({analysis.goalProgressFindings.length})
          </h2>
          <div className="space-y-3">
            {analysis.goalProgressFindings.map((f, i) => (
              <GoalProgressCard key={i} finding={f} />
            ))}
          </div>
        </section>
      )}

      {analysis.redFlags.length > 0 && (
        <Card>
          <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-3">
            Red Flags ({analysis.redFlags.length})
          </h2>
          <div className="space-y-3">
            {analysis.redFlags.map((rf, i) => (
              <div
                key={i}
                className="bg-brand-slate-50 rounded-card p-3 border-[0.5px] border-brand-slate-200"
              >
                <div className="flex items-center gap-2 mb-1">
                  <Badge
                    variant={
                      rf.severity === "high"
                        ? "error"
                        : rf.severity === "medium"
                          ? "warning"
                          : "neutral"
                    }
                  >
                    {rf.severity}
                  </Badge>
                  <Badge variant="neutral">{rf.category}</Badge>
                </div>
                <p className="text-sm font-medium text-brand-slate-800">
                  {rf.finding}
                </p>
                <p className="text-sm text-brand-slate-600 mt-1">
                  {rf.whyItMatters}
                </p>
              </div>
            ))}
          </div>
        </Card>
      )}

      {analysis.advocacyGapAnalysis && (
        <Card>
          <AdvocacyGapAnalysisSection
            gapAnalysis={analysis.advocacyGapAnalysis}
          />
        </Card>
      )}
    </div>
  );
}
