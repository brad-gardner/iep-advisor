import type { IepDocument } from "@/types/api";
import type { EtrDocument } from "@/features/etr-documents/types";
import type { ProgressReport } from "@/features/progress-reports/types";
import i18n from "@/lib/i18n";
import { formatDate } from "@/lib/format-date";
import { documentMeetingTypeLabel } from "@/lib/meeting-labels";
import { evaluationTypeLabel } from "@/features/etr-documents/lib/document-labels";

export function iepLabel(iep: IepDocument): string {
  // `formatDate(value, '')`, not the default '—' fallback: `iepDate ??
  // uploadDate` already picks the best available source, so an empty
  // result here means NEITHER date exists, and the key's `{{date}}`
  // should disappear (then `.trim()`) rather than show a bare em dash.
  const date = formatDate(iep.iepDate ?? iep.uploadDate, "");
  const type = iep.meetingType ? ` · ${documentMeetingTypeLabel(iep.meetingType)}` : "";
  return i18n.t("analysis:sourceLabel.iep", { date, type }).trim();
}

export function etrLabel(etr: EtrDocument): string {
  const date = formatDate(etr.evaluationDate ?? etr.uploadDate, "");
  const type = etr.evaluationType ? ` · ${evaluationTypeLabel(etr.evaluationType)}` : "";
  return i18n.t("analysis:sourceLabel.etr", { date, type }).trim();
}

export function progressReportLabel(report: ProgressReport): string {
  // `formatDate(value, '')` for `start`/`end` so a missing period date
  // stays falsy — the default '—' fallback would make both branches below
  // always true (an em dash is truthy), skipping the upload-date fallback
  // order entirely and showing a bare "— – —" range when no period is set.
  const start = formatDate(report.reportingPeriodStart, "");
  const end = formatDate(report.reportingPeriodEnd, "");
  if (start && end) return i18n.t("analysis:sourceLabel.progressReportRange", { start, end });
  if (start || end) return i18n.t("analysis:sourceLabel.progressReport", { date: start || end });
  return i18n.t("analysis:sourceLabel.progressReport", { date: formatDate(report.uploadDate) }).trim();
}
