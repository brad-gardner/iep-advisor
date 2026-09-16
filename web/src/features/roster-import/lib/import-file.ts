import { MAX_IMPORT_FILE_BYTES, type ImportCounts } from '../types';

// Client-side pre-checks that mirror the server's rejections so the common
// mistakes (.xlsm, oversized file) fail before an upload round-trip.
export function validateImportFile(file: File): string | null {
  if (!/\.xlsx$/i.test(file.name)) return 'Only .xlsx workbooks are accepted';
  if (file.size > MAX_IMPORT_FILE_BYTES) return 'File is larger than 5 MB';
  return null;
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function countsSummary(counts: ImportCounts): string {
  return `${counts.new} new · ${counts.updated} updated · ${counts.error} ${
    counts.error === 1 ? 'error' : 'errors'
  }`;
}
