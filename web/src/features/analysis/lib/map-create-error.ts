import i18n from "@/lib/i18n";

/**
 * Maps a failed `createRun` call to a short, user-facing message. Shared by
 * every place that starts an analysis run (the per-document hooks and the
 * child-level analysis tab) so the three copies can't drift. `i18n.t`
 * directly (not `useTranslation`): this is a plain function called from
 * event handlers and hooks alike, same reasoning as the label helpers (see
 * docs/i18n/README.md).
 */
export function mapCreateError(status: number | undefined, message?: string): string {
  if (status === 402) return i18n.t("analysis:createError.subscriptionRequired");
  if (status === 403) return i18n.t("analysis:createError.noPermission");
  return message || i18n.t("analysis:createError.generic");
}
