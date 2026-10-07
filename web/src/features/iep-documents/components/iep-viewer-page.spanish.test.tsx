import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { IepDocument } from '@/types/api';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const api = vi.hoisted(() => ({
  getIepDocument: vi.fn(),
  getIepSections: vi.fn(),
  getIepDocuments: vi.fn(),
  getDownloadUrl: vi.fn(),
  reprocessIep: vi.fn(),
}));
vi.mock('../api/iep-documents-api', () => api);
const childrenApi = vi.hoisted(() => ({ getChild: vi.fn(), setCurrentIep: vi.fn() }));
vi.mock('@/features/children/api/children-api', () => childrenApi);
const analysisRunsApi = vi.hoisted(() => ({
  getLatestForSource: vi.fn(),
  createRun: vi.fn(),
}));
vi.mock('@/features/analysis/api/analysis-runs-api', () => analysisRunsApi);
vi.mock('@/components/ui/pdf-viewer', () => ({ PdfViewer: () => <div data-testid="pdf-viewer" /> }));
vi.mock('@/features/progress-reports/components/progress-reports-tab', () => ({ ProgressReportsTab: () => null }));

import { IepViewerPage } from './iep-viewer-page';

const iep: IepDocument = {
  id: 12,
  childProfileId: 4,
  fileName: '',
  uploadDate: '2026-03-04T00:00:00Z',
  iepDate: '2026-03-03',
  status: 'parsed',
  fileSizeBytes: 1,
  meetingType: 'annual_review',
  attendees: null,
  notes: null,
  createdAt: '2026-03-04T00:00:00Z',
};

function renderPage() {
  return renderInSpanish(
    <MemoryRouter initialEntries={['/children/4/ieps/12']}>
      <Routes>
        <Route path="/children/:childId/ieps/:id" element={<IepViewerPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('IepViewerPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  beforeEach(() => {
    vi.clearAllMocks();
    api.getIepDocument.mockResolvedValue({ success: true, data: iep });
    api.getIepSections.mockResolvedValue({
      success: true,
      data: [{ id: 1, sectionType: 'annual_goals', rawText: null, parsedContent: null, displayOrder: 0, goals: [] }],
    });
    api.getIepDocuments.mockResolvedValue({ success: true, data: [iep] });
    analysisRunsApi.getLatestForSource.mockResolvedValue({ success: false });
    childrenApi.getChild.mockResolvedValue({ success: true, data: { id: 4, role: 'owner', currentIepDocumentId: 12 } });
  });

  it('renders the back link, meeting type badge and tabs in Spanish', async () => {
    await renderPage();

    expect(await screen.findByText('Volver al hijo')).toBeInTheDocument();
    // documentMeetingTypeLabel('annual_review') -> common:meetingType.AnnualReview
    // (shown twice: the header title, since fileName is null, and the badge)
    expect((await screen.findAllByText('Revisión anual')).length).toBeGreaterThan(0);
    expect(screen.getByTestId('tab-document')).toHaveTextContent('Documento');
    expect(screen.getByTestId('tab-analysis')).toHaveTextContent('Análisis');
    expect(screen.getByTestId('tab-progress-reports')).toHaveTextContent('Informes de progreso');
    expect(screen.getByRole('button', { name: /Descargar PDF/ })).toBeInTheDocument();
  });
});
