/**
 * Maps a failed `createRun` call to a short, user-facing message. Shared by
 * every place that starts an analysis run (the per-document hooks and the
 * child-level analysis tab) so the three copies can't drift.
 */
export function mapCreateError(status: number | undefined, message?: string): string {
  if (status === 402) return "Active subscription required";
  if (status === 403) return "You don't have permission";
  return message || "Could not start analysis";
}
