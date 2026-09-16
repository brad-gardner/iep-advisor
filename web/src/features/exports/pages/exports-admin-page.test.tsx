import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { ExportJobDto } from '../types';

const exportsApi = vi.hoisted(() => ({
  enqueueDistrictExport: vi.fn(),
  listDistrictExports: vi.fn(),
  getExportDownloadUrl: vi.fn(),
}));
vi.mock('../api/exports-api', () => exportsApi);

import { ExportsAdminPage } from './exports-admin-page';

function makeJob(overrides: Partial<ExportJobDto> = {}): ExportJobDto {
  return {
    id: 1,
    scope: 'District',
    districtId: 1,
    schoolStudentId: null,
    studentName: null,
    requestedByUserId: 1,
    requestedByName: 'Dana Admin',
    status: 'Queued',
    requestedAt: '2026-09-15T00:00:00.000Z',
    startedAt: null,
    completedAt: null,
    sizeBytes: null,
    error: null,
    studentCount: 0,
    fileCount: 0,
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter>
      <ExportsAdminPage />
    </MemoryRouter>
  );
}

describe('ExportsAdminPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('requests a district export and shows it Queued in the table', async () => {
    const user = userEvent.setup();
    exportsApi.listDistrictExports.mockResolvedValueOnce({ success: true, data: [] });
    exportsApi.enqueueDistrictExport.mockResolvedValue({ success: true, data: { jobId: 1 } });
    exportsApi.listDistrictExports.mockResolvedValueOnce({ success: true, data: [makeJob()] });

    renderPage();
    await waitFor(() => expect(exportsApi.listDistrictExports).toHaveBeenCalledTimes(1));

    await user.click(screen.getByTestId('request-district-export'));

    await waitFor(() => expect(exportsApi.enqueueDistrictExport).toHaveBeenCalled());
    expect(await screen.findByTestId('export-status-1')).toHaveTextContent('Queued');
  });

  it('shows a Download button only for a Completed job', async () => {
    exportsApi.listDistrictExports.mockResolvedValue({
      success: true,
      data: [makeJob({ id: 2, status: 'Completed', sizeBytes: 2048 })],
    });
    renderPage();

    expect(await screen.findByTestId('export-download-2')).toBeInTheDocument();
  });

  it('shows the error for a Failed job and no download button', async () => {
    exportsApi.listDistrictExports.mockResolvedValue({
      success: true,
      data: [makeJob({ id: 3, status: 'Failed', error: 'Blob storage unavailable' })],
    });
    renderPage();

    expect(await screen.findByTestId('export-error-3')).toHaveTextContent('Blob storage unavailable');
    expect(screen.queryByTestId('export-download-3')).not.toBeInTheDocument();
  });

  it('opens a fresh download URL for a Completed job', async () => {
    const user = userEvent.setup();
    exportsApi.listDistrictExports.mockResolvedValue({
      success: true,
      data: [makeJob({ id: 4, status: 'Completed' })],
    });
    exportsApi.getExportDownloadUrl.mockResolvedValue({ success: true, data: { url: 'https://blob/export.zip' } });
    const openSpy = vi.spyOn(window, 'open').mockImplementation(() => null);

    renderPage();
    await user.click(await screen.findByTestId('export-download-4'));

    await waitFor(() => expect(exportsApi.getExportDownloadUrl).toHaveBeenCalledWith(4));
    expect(openSpy).toHaveBeenCalledWith('https://blob/export.zip', '_blank', 'noopener,noreferrer');
    openSpy.mockRestore();
  });

  describe('polling', () => {
    beforeEach(() => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
    });

    afterEach(() => {
      act(() => {
        vi.runOnlyPendingTimers();
      });
      vi.useRealTimers();
    });

    it('polls every 10s while a job is Queued or Running', async () => {
      exportsApi.listDistrictExports.mockResolvedValue({ success: true, data: [makeJob({ status: 'Running' })] });
      renderPage();
      await waitFor(() => expect(exportsApi.listDistrictExports).toHaveBeenCalledTimes(1));

      await act(async () => {
        vi.advanceTimersByTime(10_000);
      });
      await waitFor(() => expect(exportsApi.listDistrictExports).toHaveBeenCalledTimes(2));
    });

    it('stops polling once no job is in flight', async () => {
      exportsApi.listDistrictExports.mockResolvedValue({
        success: true,
        data: [makeJob({ status: 'Completed' })],
      });
      renderPage();
      await waitFor(() => expect(exportsApi.listDistrictExports).toHaveBeenCalledTimes(1));

      await act(async () => {
        vi.advanceTimersByTime(30_000);
      });
      expect(exportsApi.listDistrictExports).toHaveBeenCalledTimes(1);
    });
  });
});
