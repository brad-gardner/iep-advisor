import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Notice } from "@/components/ui/notice";
import { Badge } from "@/components/ui/badge";
import { Spinner } from "@/components/ui/spinner";
import { AdvocacyGapAnalysisSection } from "@/features/iep-documents/components/advocacy-gap-analysis";
import { GeneratedLanguageNotice } from "@/lib/i18n/generated-language-notice";
import { useProgressReportAnalysis } from "../hooks/use-progress-report-analysis";
import { GoalProgressCard } from "./goal-progress-card";

interface ProgressReportAnalysisTabProps {
  progressReportId: number;
}

export function ProgressReportAnalysisTab({
  progressReportId,
}: ProgressReportAnalysisTabProps) {
  const { t } = useTranslation("progress-reports");
  const { analysis, status, loading, isTriggering, error, start } =
    useProgressReportAnalysis(progressReportId);

  if (loading && !analysis) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label={t("analysisTab.loading")} />
      </div>
    );
  }

  if (error && !analysis) {
    return (
      <Notice variant="error" title={t("analysisTab.loadErrorTitle")}>
        {error}
      </Notice>
    );
  }

  if (status === "none") {
    return (
      <Card className="text-center py-12">
        <h3 className="font-serif text-[20px] font-semibold text-brand-slate-800 mb-2">
          {t("analysisTab.startHeading")}
        </h3>
        <p className="text-sm text-brand-slate-500 mb-4 max-w-md mx-auto">
          {t("analysisTab.startBody")}
        </p>
        <Button onClick={start} loading={isTriggering}>
          {t("analysisTab.runAnalysis")}
        </Button>
      </Card>
    );
  }

  if (status === "pending" || status === "analyzing") {
    return (
      <Card className="text-center py-12">
        <Spinner className="mx-auto mb-4" label={t("analysisTab.analyzingLabel")} />
        <p className="text-sm text-brand-slate-500">
          {t("analysisTab.analyzingBody")}
        </p>
      </Card>
    );
  }

  if (status === "error") {
    return (
      <Card className="text-center py-12">
        <Notice variant="error" title={t("analysisTab.failedTitle")}>
          {analysis?.errorMessage || t("analysisTab.failedGeneric")}
        </Notice>
        <div className="mt-4">
          <Button onClick={start} loading={isTriggering}>
            {t("analysisTab.retryAnalysis")}
          </Button>
        </div>
      </Card>
    );
  }

  if (!analysis) return null;

  return (
    <div className="space-y-6" data-testid="progress-report-analysis">
      <GeneratedLanguageNotice generatedLanguage={analysis.generatedLanguage} />

      {analysis.summary && (
        <Card>
          <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
            {t("analysisTab.summary")}
          </h2>
          <p className="text-sm text-brand-slate-600 whitespace-pre-wrap">
            {analysis.summary}
          </p>
        </Card>
      )}

      {analysis.goalProgressFindings.length > 0 && (
        <section>
          <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-3">
            {t("analysisTab.goalProgress", { count: analysis.goalProgressFindings.length })}
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
            {t("analysisTab.redFlags", { count: analysis.redFlags.length })}
          </h2>
          <div className="space-y-3">
            {analysis.redFlags.map((rf, i) => (
              <div
                key={i}
                className="bg-brand-slate-50 rounded-card p-3 border border-brand-slate-200"
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
                    {rf.severity === "high"
                      ? t("analysisTab.severityHigh")
                      : rf.severity === "medium"
                        ? t("analysisTab.severityMedium")
                        : rf.severity === "low"
                          ? t("analysisTab.severityLow")
                          : rf.severity}
                  </Badge>
                  <Badge variant="neutral">{t(`analysisTab.category.${rf.category}`, { defaultValue: rf.category })}</Badge>
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
