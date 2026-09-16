import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { AuthoredDocumentVersionDetailDto, AuthoredDocumentVersionSummaryDto } from '../types';

const documentsApi = vi.hoisted(() => ({
  getAuthoredVersion: vi.fn(),
  listAuthoredVersions: vi.fn().mockResolvedValue({ success: true, data: [] }),
  amendVersion: vi.fn(),
  uploadSignedArtifact: vi.fn(),
  listSignedArtifacts: vi.fn().mockResolvedValue({ success: true, data: [] }),
  getSignedArtifactDownloadUrl: vi.fn(),
  getAuthoredPdfStatus: vi.fn().mockResolvedValue({
    success: true,
    data: { versionId: 1, renderStatus: 'Rendered', renderedAt: '2026-09-01T00:00:00.000Z', errorMessage: null },
  }),
  getAuthoredPdfDownloadUrl: vi.fn(),
  retryAuthoredPdf: vi.fn(),
}));
vi.mock('../api/documents-api', () => documentsApi);

import { AuthoredVersionDetailPage } from './authored-version-detail-page';

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
    <MemoryRouter initialEntries={[`/educator/students/${version.schoolStudentId}/authored-versions/${version.id}`]}>
      <Routes>
        <Route
          path="/educator/students/:studentId/authored-versions/:versionId"
          element={<AuthoredVersionDetailPage />}
        />
        <Route path="/educator/documents/:instanceId" element={<p>Document editor</p>} />
      </Routes>
    </MemoryRouter>
  );
}

describe('AuthoredVersionDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    documentsApi.listAuthoredVersions.mockResolvedValue({ success: true, data: [] });
    documentsApi.listSignedArtifacts.mockResolvedValue({ success: true, data: [] });
    documentsApi.getAuthoredPdfStatus.mockResolvedValue({
      success: true,
      data: { versionId: 1, renderStatus: 'Rendered', renderedAt: '2026-09-01T00:00:00.000Z', errorMessage: null },
    });
  });

  it('shows the signature status and amendment chain, resolving "amended by" version numbers from the sibling list', async () => {
    const versions: AuthoredDocumentVersionSummaryDto[] = [
      {
        id: 4,
        schoolStudentId: 10,
        documentTypeId: 1,
        documentTypeKey: 'IEP',
        documentTypeDisplayName: 'IEP',
        versionNumber: 4,
        finalizedByUserId: 1,
        finalizedAt: '2026-09-10T00:00:00.000Z',
        pdfRenderStatus: 'Rendered',
        signatureStatus: 'Unsigned',
        signedArtifactCount: 0,
        amendsVersionId: 3,
        amendsVersionNumber: 3,
        amendmentReason: null,
        effectiveDate: null,
        amendedByVersionIds: [],
      },
    ];
    documentsApi.listAuthoredVersions.mockResolvedValue({ success: true, data: versions });
    // The version being viewed (v3) amends v2 (its own field carries the number
    // directly) and is amended by v4 (only its id is on the DTO — the page
    // resolves v4's number from the sibling list above).
    const version = makeVersion({
      id: 3,
      versionNumber: 3,
      amendsVersionId: 2,
      amendsVersionNumber: 2,
      amendedByVersionIds: [4],
    });
    renderPage(version);

    expect(await screen.findByTestId('version-signature-status')).toHaveTextContent('Unsigned');
    const chain = screen.getByTestId('version-amendment-chain');
    expect(chain).toHaveTextContent('Amends v2');
    expect(chain).toHaveTextContent('Amended by');
    expect(chain).toHaveTextContent('v4');
  });

  it('attaches a signed PDF and updates the signature status', async () => {
    const user = userEvent.setup();
    renderPage(makeVersion());
    documentsApi.uploadSignedArtifact.mockResolvedValue({
      success: true,
      data: {
        id: 5,
        authoredDocumentVersionId: 1,
        fileName: 'signed.pdf',
        contentType: 'application/pdf',
        sizeBytes: 1024,
        uploadedByUserId: 1,
        uploadedByName: 'Casey Manager',
        uploadedAt: '2026-09-02T00:00:00.000Z',
        signerSummary: 'Parent and LEA rep signed',
      },
    });

    await screen.findByTestId('signed-artifacts-panel');
    const file = new File(['%PDF-1.4'], 'signed.pdf', { type: 'application/pdf' });
    await user.upload(screen.getByTestId('signed-artifact-file'), file);
    await user.selectOptions(screen.getByTestId('signed-artifact-status'), 'Signed');
    await user.click(screen.getByTestId('signed-artifact-submit'));

    await waitFor(() => expect(documentsApi.uploadSignedArtifact).toHaveBeenCalledWith(1, file, 'Signed', undefined));
    expect(await screen.findByTestId('version-signature-status')).toHaveTextContent('Signed');
    expect(await screen.findByTestId('signed-artifact-download-5')).toBeInTheDocument();
  });

  it('amends the version and navigates to the new draft', async () => {
    const user = userEvent.setup();
    renderPage(makeVersion());
    documentsApi.amendVersion.mockResolvedValue({ success: true, data: { instanceId: 777 } });

    await user.click(await screen.findByTestId('amend-open'));
    await user.type(screen.getByTestId('amend-dialog-reason'), 'District revised the placement recommendation.');
    await user.click(screen.getByTestId('amend-dialog-submit'));

    await waitFor(() =>
      expect(documentsApi.amendVersion).toHaveBeenCalledWith(1, {
        reason: 'District revised the placement recommendation.',
        effectiveDate: undefined,
      })
    );
    expect(await screen.findByText('Document editor')).toBeInTheDocument();
  });
});
