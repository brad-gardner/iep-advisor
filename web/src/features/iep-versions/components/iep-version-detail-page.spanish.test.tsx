import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { IepVersionDto } from '../types';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const api = vi.hoisted(() => ({
  getVersion: vi.fn(),
  getPdfStatus: vi.fn(),
  retryPdf: vi.fn(),
}));
vi.mock('../api/iep-versions-api', () => api);

import { ParentVersionDetailPage } from './parent-version-detail-page';

const version: IepVersionDto = {
  id: 9,
  schoolStudentId: 1,
  sourceDraftId: 1,
  versionNumber: 2,
  documentType: 'IEP',
  title: null,
  effectiveDate: null,
  finalizedByUserId: 1,
  finalizedAt: '2026-03-01T00:00:00Z',
  pdfRenderStatus: 'Rendered',
  pdfBlobUri: null,
  pdfRenderedAt: null,
  sections: [],
  goals: [],
  serviceLines: [],
  accommodations: [],
  transitionItems: [],
};

describe('IepVersionDetailPage (parent) in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the back link and download button in Spanish', async () => {
    api.getVersion.mockResolvedValue({ success: true, data: version });
    api.getPdfStatus.mockResolvedValue({
      success: true,
      data: { versionId: 9, renderStatus: 'Rendered', url: 'https://example.com/v9.pdf', renderedAt: null, errorMessage: null },
    });

    await renderInSpanish(
      <MemoryRouter initialEntries={['/children/4/iep-versions/9']}>
        <Routes>
          <Route path="/children/:childId/iep-versions/:versionId" element={<ParentVersionDetailPage />} />
        </Routes>
      </MemoryRouter>,
      { ns: 'iep-versions' },
    );

    expect(await screen.findByText('Volver al hijo')).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: 'Descargar PDF' })).toBeInTheDocument();
  });
});
