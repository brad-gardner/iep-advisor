import '@/app/lazy-routes/staff-locales';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { ExportJobDto } from '../types';

const exportsApi = vi.hoisted(() => ({
  enqueueDistrictExport: vi.fn(),
  listDistrictExports: vi.fn(),
  getExportDownloadUrl: vi.fn(),
}));
vi.mock('../api/exports-api', () => exportsApi);

function makeJob(overrides: Partial<ExportJobDto> = {}): ExportJobDto {
  return {
    id: 1,
    scope: 'District',
    districtId: 1,
    schoolStudentId: null,
    studentName: null,
    requestedByUserId: 1,
    requestedByName: 'Dana Admin',
    status: 'Completed',
    requestedAt: '2026-09-15T00:00:00.000Z',
    startedAt: null,
    completedAt: null,
    sizeBytes: 2048,
    error: null,
    studentCount: 10,
    fileCount: 20,
    ...overrides,
  };
}

import { ExportsAdminPage } from './exports-admin-page';

describe('ExportsAdminPage in Spanish', () => {
  beforeEach(() => {
    Object.values(exportsApi).forEach((fn) => fn.mockReset());
    exportsApi.listDistrictExports.mockResolvedValue({ success: true, data: [makeJob()] });
  });

  afterEach(() => resetTestLanguage());

  it('renders the heading, request button and job status in Spanish', async () => {
    await renderInSpanish(
      <MemoryRouter>
        <ExportsAdminPage />
      </MemoryRouter>,
      { ns: 'exports' }
    );

    expect(screen.getByRole('heading', { name: 'Exportaciones' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Solicitar exportación del distrito' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('export-status-1')).toHaveTextContent('Completado'));
    expect(screen.getByTestId('export-download-1')).toHaveTextContent('Descargar');
  });
});
