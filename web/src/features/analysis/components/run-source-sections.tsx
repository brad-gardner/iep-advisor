import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Card } from "@/components/ui/card";
import { Notice } from "@/components/ui/notice";
import { AnalysisGoalsList } from "@/features/iep-documents/components/analysis-goals-list";
import { RunSectionDetail } from "./run-section-detail";
import { RunStatusBadge } from "./run-status-badge";
import type { AnalysisRunSection, AnalysisRunSource } from "../types";

interface RunSourceSectionsProps {
  childId: number;
  source: AnalysisRunSource;
  sections: AnalysisRunSection[];
  /** Shows an "Ask the advocate" launcher on each goal card (parents who can ask). */
  canAsk?: boolean;
}

/**
 * Where a source's header links to. Progress report sources need the IEP id
 * their viewer route is nested under, which this model doesn't carry, so
 * they (and any other source kind without a document route of their own)
 * render as a plain label instead of a guessed link.
 */
function sourceDocumentHref(childId: number, source: AnalysisRunSource): string | null {
  switch (source.sourceType) {
    case "IepDocument":
      return `/children/${childId}/ieps/${source.sourceId}`;
    case "EtrDocument":
      return `/children/${childId}/etrs/${source.sourceId}`;
    default:
      return null;
  }
}

export function RunSourceSections({
  childId,
  source,
  sections,
  canAsk = false,
}: RunSourceSectionsProps) {
  const { t } = useTranslation("analysis");
  const ordered = [...sections].sort((a, b) => a.displayOrder - b.displayOrder);
  const label = source.sourceLabel ?? `${source.sourceType} #${source.sourceId}`;
  const href = sourceDocumentHref(childId, source);
  const goalAnalyses = ordered.find((s) => s.sectionKind === "iep_goals")?.goalAnalyses;
  const isFailed = source.status === "Error";
  const isInFlight = source.status === "Pending" || source.status === "Running";

  return (
    <Card data-testid={`analysis-source-${source.id}`}>
      <div className="flex flex-wrap items-center gap-2 mb-4">
        <h2 className="font-serif text-[22px] font-semibold text-brand-slate-800">
          {href ? (
            <Link
              to={href}
              className="underline decoration-brand-slate-300 underline-offset-4 hover:text-brand-teal-600 hover:decoration-brand-teal-400 rounded-sm focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-teal-500"
            >
              {label}
            </Link>
          ) : (
            label
          )}
        </h2>
        {isInFlight && <RunStatusBadge status={source.status} />}
      </div>

      {isFailed && (
        <Notice
          variant="warning"
          title={t("sourceSections.failedTitle")}
        >
          {source.errorMessage}
        </Notice>
      )}

      {!isFailed && (
        <div className="space-y-8">
          {goalAnalyses && goalAnalyses.length > 0 && (
            <AnalysisGoalsList
              goalAnalyses={goalAnalyses}
              childId={childId}
              canAsk={canAsk}
              headingLevel={3}
            />
          )}
          {ordered.map((section) =>
            section.analysis ? (
              <RunSectionDetail key={section.id} section={section.analysis} />
            ) : null
          )}
        </div>
      )}
    </Card>
  );
}
