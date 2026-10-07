import i18n from '@/lib/i18n';
import { MAX_IMPORT_FILE_BYTES, type ImportCounts } from '../types';

// Client-side pre-checks that mirror the server's rejections so the common
// mistakes (.xlsm, oversized file) fail before an upload round-trip.
//
// These call `i18n.t` directly (the `orgRoleLabel` shape), rather than
// `useTranslation`, since they're plain functions called from event handlers
// and other plain functions alike, not render bodies — see
// `docs/i18n/README.md`'s "Display-label helpers" and "A plain `i18n.t`
// helper doesn't load anything by itself" (the caller's OWN `useTranslation`
// call, e.g. `upload-step.tsx`'s `useTranslation('roster-import')`, is what
// actually subscribes to and loads this namespace).
export function validateImportFile(file: File): string | null {
  if (!/\.xlsx$/i.test(file.name)) return i18n.t('roster-import:file.onlyXlsx');
  if (file.size > MAX_IMPORT_FILE_BYTES) return i18n.t('roster-import:file.tooLarge');
  return null;
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function countsSummary(counts: ImportCounts): string {
  return i18n.t('roster-import:file.countsSummary', {
    newCount: counts.new,
    updatedCount: counts.updated,
    errorCount: counts.error,
    count: counts.error,
  });
}
