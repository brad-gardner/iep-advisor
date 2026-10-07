import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { TemplateVersionDetailDto } from '@/features/admin/templates/types';
import type { SharedDraftRevisionDetailDto } from '../types';

const sharedDraftsApi = vi.hoisted(() => ({
  getSharedDraft: vi.fn(),
  getSharedDrafts: vi.fn(),
  getDraftNotes: vi.fn(),
  getDraftResponses: vi.fn(),
  getDraftExplanations: vi.fn(),
  askDraftQuestion: vi.fn(),
  createDraftResponse: vi.fn(),
  acknowledgeSharedDraft: vi.fn(),
  deleteDraftNote: vi.fn(),
}));
vi.mock('../api/shared-drafts-api', () => sharedDraftsApi);

import { SharedDraftReviewPage } from './shared-draft-review-page';

const templateVersion: TemplateVersionDetailDto = {
  id: 10,
  documentTemplateId: 1,
  versionNumber: 1,
  status: 'Published',
  publishedAt: '2026-01-01T00:00:00.000Z',
  rowVersion: null,
  sections: [
    {
      id: 1,
      sectionKey: 'goals',
      title: 'Goals',
      displayOrder: 0,
      fields: [
        {
          id: 100,
          fieldKey: 'goals-field',
          fieldType: 'Table',
          label: 'Annual goals',
          required: false,
          displayOrder: 0,
          configJson: JSON.stringify({
            semantic: 'goals',
            columns: [{ columnKey: 'goalText', type: 'Text', label: 'Goal', required: true, semantic: 'goalText' }],
          }),
        },
      ],
    },
  ],
};

function makeDetail(overrides: Partial<SharedDraftRevisionDetailDto> = {}): SharedDraftRevisionDetailDto {
  return {
    id: 55,
    documentInstanceId: 7,
    studentId: 3,
    studentName: 'Alex Student',
    documentTypeKey: 'iep',
    documentTypeDisplayName: 'IEP',
    revisionNumber: 2,
    status: 'Active',
    sharedAt: '2026-09-10T00:00:00.000Z',
    sharedByName: 'Case Manager',
    message: null,
    withdrawnAt: null,
    changeSummary: null,
    acknowledgedAt: null,
    openResponseCount: 0,
    templateVersionId: 10,
    values: {
      'goals-field': [{ _rowId: 'row-1', goalText: 'Improve reading fluency' }],
    },
    templateVersion,
    ...overrides,
  };
}

function renderPage() {
  return renderInSpanish(
    <ToastProvider>
      <MemoryRouter initialEntries={['/children/3/shared-drafts/55']}>
        <Routes>
          <Route path="/children/:childId/shared-drafts/:rev" element={<SharedDraftReviewPage />} />
        </Routes>
      </MemoryRouter>
    </ToastProvider>
  );
}

describe('SharedDraftReviewPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading, goal card and an AI explanation (with its generated-language notice) in Spanish', async () => {
    vi.clearAllMocks();
    sharedDraftsApi.getSharedDrafts.mockResolvedValue({ success: true, data: [] });
    sharedDraftsApi.getDraftNotes.mockResolvedValue({ success: true, data: [] });
    sharedDraftsApi.getDraftResponses.mockResolvedValue({ success: true, data: [] });
    sharedDraftsApi.getSharedDraft.mockResolvedValue({ success: true, data: makeDetail() });
    sharedDraftsApi.getDraftExplanations.mockResolvedValue({
      success: true,
      data: {
        revisionId: 55,
        generatedAt: '2026-09-10T00:00:00.000Z',
        sections: [],
        items: [{ fieldKey: 'goals-field', rowId: 'row-1', label: 'Improve reading fluency', explanation: 'Targets reading speed.' }],
        disclaimer: 'AI-generated, not legal advice.',
        generatedLanguage: 'en',
      },
    });
    const user = userEvent.setup();

    await renderPage();

    expect(await screen.findByTestId('draft-item-card-goals-field-row-1')).toHaveTextContent('Improve reading fluency');
    expect(screen.getByRole('button', { name: 'Hacer una pregunta' })).toBeInTheDocument();

    await user.click(screen.getByTestId('explain-goals-field-row-1'));
    expect(await screen.findByText('Targets reading speed.')).toBeInTheDocument();
    expect(screen.getByTestId('generated-language-notice')).toHaveTextContent('Generado en inglés');
  });

  it('renders the withdrawn-revision banner in Spanish', async () => {
    vi.clearAllMocks();
    sharedDraftsApi.getSharedDrafts.mockResolvedValue({ success: true, data: [] });
    sharedDraftsApi.getDraftNotes.mockResolvedValue({ success: true, data: [] });
    sharedDraftsApi.getDraftResponses.mockResolvedValue({ success: true, data: [] });
    sharedDraftsApi.getSharedDraft.mockResolvedValue({
      success: true,
      data: makeDetail({ status: 'Withdrawn', withdrawnAt: '2026-09-12T00:00:00.000Z' }),
    });

    await renderPage();

    expect(await screen.findByTestId('revision-withdrawn-banner')).toHaveTextContent('Este borrador fue retirado');
  });
});
