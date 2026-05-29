import type { IepDocument } from "@/types/api";
import type { EtrDocument } from "@/features/etr-documents/types";
import type { ProgressReport } from "@/features/progress-reports/types";
import { EVALUATION_TYPE_LABELS } from "@/features/etr-documents/types";

function formatDate(value: string | null): string {
  if (!value) return "";
  return new Date(value).toLocaleDateString();
}

export function iepLabel(iep: IepDocument): string {
  const date = formatDate(iep.iepDate ?? iep.uploadDate);
  const type = iep.meetingType ? ` · ${iep.meetingType}` : "";
  return `IEP ${date}${type}`.trim();
}

export function etrLabel(etr: EtrDocument): string {
  const date = formatDate(etr.evaluationDate ?? etr.uploadDate);
  const type = etr.evaluationType
    ? ` · ${EVALUATION_TYPE_LABELS[etr.evaluationType] ?? etr.evaluationType}`
    : "";
  return `ETR ${date}${type}`.trim();
}

export function progressReportLabel(report: ProgressReport): string {
  const start = formatDate(report.reportingPeriodStart);
  const end = formatDate(report.reportingPeriodEnd);
  if (start && end) return `Progress Report ${start} – ${end}`;
  if (start || end) return `Progress Report ${start || end}`;
  return `Progress Report ${formatDate(report.uploadDate)}`.trim();
}
