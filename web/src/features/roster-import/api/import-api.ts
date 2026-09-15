import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import { fileNameFromDisposition } from '@/lib/download-file';
import type {
  CommitImportRequest,
  ImportBatch,
  ImportKind,
  ImportPreview,
  ImportResult,
} from '../types';

const IMPORTS = '/api/district/imports';

export interface DownloadedFile {
  blob: Blob;
  fileName: string;
}

function kindSegment(kind: ImportKind): 'students' | 'staff' {
  return kind === 'Staff' ? 'staff' : 'students';
}

async function downloadFile(url: string, fallbackName: string): Promise<DownloadedFile> {
  const response = await apiClient.get<Blob>(url, { responseType: 'blob' });
  const disposition = response.headers?.['content-disposition'] as string | undefined;
  return { blob: response.data, fileName: fileNameFromDisposition(disposition, fallbackName) };
}

// Server-generated workbook: the `Values` sheet lists live allowed values
// (schools, case managers, roles), so it is fetched fresh each time.
export async function downloadImportTemplate(kind: ImportKind): Promise<DownloadedFile> {
  return downloadFile(
    `${IMPORTS}/${kindSegment(kind)}/template`,
    `${kindSegment(kind)}-import-template.xlsx`
  );
}

export async function previewImport(
  kind: ImportKind,
  file: File
): Promise<ApiResponse<ImportPreview>> {
  const form = new FormData();
  form.append('file', file);
  const response = await apiClient.post<ApiResponse<ImportPreview>>(
    `${IMPORTS}/${kindSegment(kind)}/preview`,
    form,
    { headers: { 'Content-Type': 'multipart/form-data' } }
  );
  return response.data;
}

export async function commitImport(
  batchId: number,
  data: CommitImportRequest
): Promise<ApiResponse<ImportResult>> {
  const response = await apiClient.post<ApiResponse<ImportResult>>(
    `${IMPORTS}/${batchId}/commit`,
    data
  );
  return response.data;
}

export async function getImportBatches(): Promise<ApiResponse<ImportBatch[]>> {
  const response = await apiClient.get<ApiResponse<ImportBatch[]>>(IMPORTS);
  return response.data;
}

export async function getImportBatch(batchId: number): Promise<ApiResponse<ImportPreview>> {
  const response = await apiClient.get<ApiResponse<ImportPreview>>(`${IMPORTS}/${batchId}`);
  return response.data;
}

export async function downloadImportErrors(batchId: number): Promise<DownloadedFile> {
  return downloadFile(`${IMPORTS}/${batchId}/errors.xlsx`, 'import-errors.xlsx');
}
