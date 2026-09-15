/**
 * Hand a fetched binary (e.g. an `.xlsx` from `apiClient.get(..., {
 * responseType: 'blob' })`) to the browser as a named download. Creates and
 * revokes the object URL in one go so nothing leaks.
 */
export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  URL.revokeObjectURL(url);
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
