/**
 * Hand a fetched binary (e.g. an `.xlsx` from `apiClient.get(..., {
 * responseType: 'blob' })`) to the browser as a named download. The object
 * URL is revoked on a later task: revoking in the same task as `click()` can
 * abort the download in some browsers (Safari historically).
 */
export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

/**
 * Pull the server-suggested filename out of a `Content-Disposition` header,
 * falling back to `fallback` when absent/unparseable.
 */
export function fileNameFromDisposition(
  disposition: string | null | undefined,
  fallback: string
): string {
  if (!disposition) return fallback;
  const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (utf8?.[1]) {
    try {
      return decodeURIComponent(utf8[1]);
    } catch {
      return fallback;
    }
  }
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain?.[1]?.trim() || fallback;
}
