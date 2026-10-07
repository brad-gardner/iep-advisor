import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Card } from "@/components/ui/card";
import { Notice } from "@/components/ui/notice";
import { RedFlagCard } from "@/features/iep-documents/components/red-flag-card";
import { AdvocacyGapAnalysisSection } from "@/features/iep-documents/components/advocacy-gap-analysis";
import { AskAdvocateButton } from "@/features/advocate/components/ask-advocate-button";
import { GeneratedLanguageNotice } from "@/lib/i18n/generated-language-notice";
import { formatDate } from "@/lib/format-date";
import { useAnalysisRun } from "../hooks/use-analysis-run";
import { RunStatusBadge } from "./run-status-badge";
import { RunSourceSections } from "./run-source-sections";
import { CrossDocSynthesisSection } from "./cross-doc-synthesis-section";
import type { AnalysisRunSection } from "../types";

interface RunDetailProps {
  childId: number;
  runId: number;
  /** Shows the "Ask the advocate" launcher for this run (parents who can ask). */
  canAsk?: boolean;
}

export function RunDetail({ childId, runId, canAsk = false }: RunDetailProps) {
  const { t } = useTranslation("analysis");
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
        <p className="text-sm text-brand-slate-500">{t("runDetail.loading")}</p>
      </Card>
    );
  }

  if (!run) {
    return (
      <Card>
        <p className="text-sm text-brand-slate-500">{t("runDetail.selectPrompt")}</p>
      </Card>
    );
  }

  const isComplete = run.status === "Completed";
  const isError = run.status === "Error";
  const isInFlight = run.status === "Pending" || run.status === "Running";

  const completedOrErroredSources = run.sources.filter(
    (source) => source.status === "Completed" || source.status === "Error"
  ).length;
  const sourceProgressLabel =
    run.sources.length > 0
      ? t("runDetail.sourcesProgress", { completed: completedOrErroredSources, total: run.sources.length })
      : null;

  return (
    <div className="space-y-6" data-testid="analysis-run-detail">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="font-serif">{t("runDetail.heading")}</h2>
        <RunStatusBadge status={run.status} />
        {canAsk && isComplete && (
          <AskAdvocateButton
            childId={childId}
            about={{ kind: "analysis", id: run.id }}
            label={t("runDetail.askAbout", { date: formatDate(run.createdAt) })}
            className="ml-auto"
            data-testid="analysis-ask-advocate"
          />
        )}
      </div>

      {isComplete && (
        <GeneratedLanguageNotice generatedLanguage={run.generatedLanguage} />
      )}

      {isError && (
        <Notice variant="error" title={t("runDetail.failedTitle")}>
          {run.errorMessage ?? t("runDetail.failedGeneric")}
        </Notice>
      )}

      {isInFlight && pollTimedOut && (
        <Notice variant="info" title={t("runDetail.stillWorkingTitle")}>
          {t("runDetail.stillWorkingBody", { progress: sourceProgressLabel ? ` ${sourceProgressLabel}` : "" })}
        </Notice>
      )}

      {isInFlight && !pollTimedOut && (
        <Notice variant="info" title={t("runDetail.inProgressTitle")}>
          {sourceProgressLabel
            ? t("runDetail.inProgressWithProgress", { progress: sourceProgressLabel })
            : t("runDetail.inProgressGeneric")}
        </Notice>
      )}

      {isComplete && run.errorMessage && (
        <Notice variant="info" title={t("runDetail.completedWithNoteTitle")}>
          {run.errorMessage}
        </Notice>
      )}

      {isComplete && (
        <>
          {run.overallSummary && (
            <Card>
              <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800 mb-2">
                {t("runDetail.summary")}
              </h2>
              <p className="text-sm text-brand-slate-600 leading-relaxed">
                {run.overallSummary}
              </p>
            </Card>
          )}

          {run.sources.map((source) => (
            <RunSourceSections
              key={source.id}
              childId={childId}
              source={source}
              sections={sectionsBySource.get(source.id) ?? []}
              canAsk={canAsk}
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
                {t("runDetail.overallConcerns")}
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
