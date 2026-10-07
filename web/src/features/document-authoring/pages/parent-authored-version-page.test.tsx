import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { AuthoredDocumentVersionDetailDto } from '../types';

// Deliberately NOT importing `@/app/lazy-routes/staff-locales` here (unlike
// `authored-version-detail-page.test.tsx`): this page renders on the PARENT
// route, which must never depend on the staff-only lazy chunk. The whole
// point of this test is to prove `AuthoredPdfDownload` (rendered via
// `actions={...}` below) works with no staff namespace registered — see
// `authored-pdf-download.tsx`'s doc comment and `docs/i18n/README.md`'s
// "Staff and admin namespaces" section.

const documentsApi = vi.hoisted(() => ({
  getAuthoredVersion: vi.fn(),
  getAuthoredPdfStatus: vi.fn(),
  retryAuthoredPdf: vi.fn(),
  getAuthoredPdfDownloadUrl: vi.fn(),
}));
vi.mock('../api/documents-api', () => documentsApi);

import { ParentAuthoredVersionPage } from './parent-authored-version-page';

function makeVersion(overrides: Partial<AuthoredDocumentVersionDetailDto> = {}): AuthoredDocumentVersionDetailDto {
  return {
    id: 1,
    schoolStudentId: 10,
    documentTypeId: 1,
    documentTypeKey: 'IEP',
    documentTypeDisplayName: 'IEP',
    documentTemplateVersionId: 1,
    versionNumber: 3,
    finalizedByUserId: 1,
    finalizedAt: '2026-09-01T00:00:00.000Z',
    values: {},
    pdfRenderStatus: 'Rendered',
    pdfBlobUri: null,
    pdfRenderedAt: '2026-09-01T00:00:00.000Z',
    signatureStatus: 'Unsigned',
    signedArtifactCount: 0,
    amendsVersionId: null,
    amendsVersionNumber: null,
    amendmentReason: null,
    effectiveDate: null,
    amendedByVersionIds: [],
    templateVersion: {
      id: 1,
      documentTemplateId: 1,
      versionNumber: 1,
      status: 'Published',
      publishedAt: '2026-01-01T00:00:00.000Z',
      rowVersion: null,
      sections: [],
    },
    ...overrides,
  };
}

function renderPage(version: AuthoredDocumentVersionDetailDto) {
  documentsApi.getAuthoredVersion.mockResolvedValue({ success: true, data: version });
  return render(
    <MemoryRouter initialEntries={[`/children/${version.schoolStudentId}/authored-versions/${version.id}`]}>
      <Routes>
        <Route path="/children/:childId/authored-versions/:versionId" element={<ParentAuthoredVersionPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('ParentAuthoredVersionPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    documentsApi.getAuthoredPdfStatus.mockResolvedValue({
      success: true,
      data: { versionId: 1, renderStatus: 'Rendered', renderedAt: '2026-09-01T00:00:00.000Z', errorMessage: null },
    });
  });

  it('shows the Download PDF action in English with no staff namespace registered', async () => {
    renderPage(makeVersion());

    expect(await screen.findByRole('button', { name: 'Download PDF' })).toBeInTheDocument();
  });
});
