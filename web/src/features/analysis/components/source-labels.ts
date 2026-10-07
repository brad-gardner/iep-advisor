import type { IepDocument } from "@/types/api";
import type { EtrDocument } from "@/features/etr-documents/types";
import type { ProgressReport } from "@/features/progress-reports/types";
import i18n from "@/lib/i18n";
import { formatDate } from "@/lib/format-date";
import { documentMeetingTypeLabel } from "@/lib/meeting-labels";
import { evaluationTypeLabel } from "@/features/etr-documents/lib/document-labels";

export function iepLabel(iep: IepDocument): string {
  const date = formatDate(iep.iepDate ?? iep.uploadDate);
  const type = iep.meetingType ? ` · ${documentMeetingTypeLabel(iep.meetingType)}` : "";
  return i18n.t("analysis:sourceLabel.iep", { date, type }).trim();
}

export function etrLabel(etr: EtrDocument): string {
  const date = formatDate(etr.evaluationDate ?? etr.uploadDate);
  const type = etr.evaluationType ? ` · ${evaluationTypeLabel(etr.evaluationType)}` : "";
  return i18n.t("analysis:sourceLabel.etr", { date, type }).trim();
}

export function progressReportLabel(report: ProgressReport): string {
  const start = formatDate(report.reportingPeriodStart);
  const end = formatDate(report.reportingPeriodEnd);
  if (start && end) return i18n.t("analysis:sourceLabel.progressReportRange", { start, end });
  if (start || end) return i18n.t("analysis:sourceLabel.progressReport", { date: start || end });
  return i18n.t("analysis:sourceLabel.progressReport", { date: formatDate(report.uploadDate) }).trim();
}
