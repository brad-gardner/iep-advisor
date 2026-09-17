import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { ProgressReport } from '../types';

const progressReportsApi = vi.hoisted(() => ({
  getById: vi.fn(),
  getDownloadUrl: vi.fn(),
}));
vi.mock('../api/progress-reports-api', () => progressReportsApi);

import { ProgressReportViewerPage } from './progress-report-viewer-page';

function makeReport(overrides: Partial<ProgressReport> = {}): ProgressReport {
  return {
    id: 1,
    iepDocumentId: 5,
    childProfileId: 3,
    fileName: 'q1-progress.pdf',
    uploadDate: '2026-01-01T00:00:00.000Z',
    reportingPeriodStart: '2026-01-01',
    reportingPeriodEnd: '2026-03-01',
    notes: null,
    status: 'parsed',
    errorMessage: null,
    fileSizeBytes: 1024,
    createdAt: '2026-01-01T00:00:00.000Z',
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/children/3/ieps/5/progress-reports/1']}>
      <Routes>
        <Route
          path="/children/:childId/ieps/:id/progress-reports/:prId"
          element={<ProgressReportViewerPage />}
        />
      </Routes>
    </MemoryRouter>
  );
}

describe('ProgressReportViewerPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    progressReportsApi.getDownloadUrl.mockResolvedValue({ success: true, data: { url: 'https://example.com/report.pdf' } });
  });

  it('renders a stored markdown note as formatted HTML', async () => {
    progressReportsApi.getById.mockResolvedValue({
      success: true,
      data: makeReport({ notes: 'Reviewed with the **case manager** before upload.' }),
    });

    renderPage();

    const strong = await screen.findByText('case manager');
    expect(strong.tagName).toBe('STRONG');
  });

  it('renders nothing for the notes block when there are no notes', async () => {
    progressReportsApi.getById.mockResolvedValue({
      success: true,
      data: makeReport({ notes: null }),
    });

    renderPage();

    await screen.findByTestId('pr-tab-document');
    expect(screen.queryByText(/case manager/i)).not.toBeInTheDocument();
  });
});
