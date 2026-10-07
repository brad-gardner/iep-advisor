import { useTranslation } from "react-i18next";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import type { GoalProgressFinding } from "../types";

interface GoalProgressCardProps {
  finding: GoalProgressFinding;
}

export function GoalProgressCard({ finding }: GoalProgressCardProps) {
  const { t } = useTranslation("progress-reports");
  const RATING_VARIANTS: Record<
    string,
    { variant: "neutral" | "warning" | "success" | "error"; label: string }
  > = {
    met: { variant: "success", label: t("goalProgressCard.ratingMet") },
    on_track: { variant: "success", label: t("goalProgressCard.ratingOnTrack") },
    concerning: { variant: "warning", label: t("goalProgressCard.ratingConcerning") },
    regressing: { variant: "error", label: t("goalProgressCard.ratingRegressing") },
    insufficient_data: { variant: "neutral", label: t("goalProgressCard.ratingInsufficientData") },
  };

  const EVIDENCE_LABELS: Record<string, string> = {
    strong: t("goalProgressCard.evidenceStrong"),
    adequate: t("goalProgressCard.evidenceAdequate"),
    weak: t("goalProgressCard.evidenceWeak"),
  };

  const rating = RATING_VARIANTS[finding.progressRating] ?? {
    variant: "neutral" as const,
    label: finding.progressRating,
  };

  return (
    <Card className="p-4 space-y-3">
      <div>
        <div className="flex items-center gap-2 flex-wrap mb-2">
          <Badge variant={rating.variant}>{rating.label}</Badge>
          <Badge variant="neutral">
            {EVIDENCE_LABELS[finding.evidenceQuality] ?? finding.evidenceQuality}
          </Badge>
          {finding.domain && <Badge variant="neutral">{finding.domain}</Badge>}
        </div>
        <p className="text-sm font-medium text-brand-slate-800">
          {finding.iepGoalText}
        </p>
      </div>

      <div>
        <p className="text-[11px] text-brand-slate-500 uppercase tracking-wide font-semibold mb-1">
          {t("goalProgressCard.whatReportSays")}
        </p>
        <p className="text-sm text-brand-slate-600 whitespace-pre-wrap">
          {finding.reportedProgress}
        </p>
      </div>

      {finding.redFlags.length > 0 && (
        <div>
          <p className="text-[11px] text-brand-danger-700 uppercase tracking-wide font-semibold mb-1">
            {t("goalProgressCard.concerns")}
          </p>
          <ul className="text-sm text-brand-slate-600 list-disc pl-5 space-y-1">
            {finding.redFlags.map((flag, i) => (
              <li key={i}>{flag}</li>
            ))}
          </ul>
        </div>
      )}

      {finding.parentTalkingPoints.length > 0 && (
        <div>
          <p className="text-[11px] text-brand-teal-600 uppercase tracking-wide font-semibold mb-1">
            {t("goalProgressCard.whatYouCanAsk")}
          </p>
          <ul className="text-sm text-brand-slate-600 list-disc pl-5 space-y-1">
            {finding.parentTalkingPoints.map((p, i) => (
              <li key={i}>{p}</li>
            ))}
          </ul>
        </div>
      )}
    </Card>
  );
}
