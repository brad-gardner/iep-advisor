import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import type { TemplateVersionDetailDto } from '@/features/admin/templates/types';
import type { DraftAnswerDto, DraftResponseDto, SharedDraftRevisionDetailDto } from '../types';

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
            columns: [
              { columnKey: 'goalText', type: 'Text', label: 'Goal', required: true, semantic: 'goalText' },
              { columnKey: 'baseline', type: 'Text', label: 'Baseline', required: false, semantic: 'baseline' },
            ],
          }),
        },
      ],
    },
    {
      id: 2,
      sectionKey: 'present-levels',
      title: 'Present levels',
      displayOrder: 1,
      fields: [
        {
          id: 101,
          fieldKey: 'plaafp',
          fieldType: 'Text',
          label: 'Present levels',
          required: false,
          displayOrder: 0,
          configJson: JSON.stringify({ semantic: 'presentLevels' }),
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
      'goals-field': [
        { _rowId: 'row-1', goalText: 'Improve reading fluency', baseline: 'Reads 60 wpm' },
        { _rowId: 'row-2', goalText: 'Improve math skills', baseline: 'Solves 5/10 problems' },
      ],
      plaafp: 'Student reads below grade level.',
    },
    templateVersion,
    ...overrides,
  };
}

function renderPage() {
  render(
    <ToastProvider>
      <MemoryRouter initialEntries={['/children/3/shared-drafts/55']}>
        <Routes>
          <Route path="/children/:childId/shared-drafts/:rev" element={<SharedDraftReviewPage />} />
        </Routes>
      </MemoryRouter>
    </ToastProvider>
  );
}

describe('SharedDraftReviewPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sharedDraftsApi.getSharedDrafts.mockResolvedValue({ success: true, data: [] });
    sharedDraftsApi.getDraftNotes.mockResolvedValue({ success: true, data: [] });
    sharedDraftsApi.getDraftResponses.mockResolvedValue({ success: true, data: [] });
  });

  it('renders the frozen goal rows as cards and the other fields via the shared snapshot renderer', async () => {
    sharedDraftsApi.getSharedDraft.mockResolvedValue({ success: true, data: makeDetail() });
    renderPage();

    expect(await screen.findByTestId('draft-item-card-goals-field-row-1')).toHaveTextContent('Improve reading fluency');
    expect(screen.getByTestId('draft-item-card-goals-field-row-2')).toHaveTextContent('Improve math skills');
    expect(screen.getByTestId('frozen-section-list')).toHaveTextContent('Student reads below grade level.');
  });

  it('caches the explanation fetch across cards: one network call, both cards show their own text', async () => {
    const user = userEvent.setup();
    sharedDraftsApi.getSharedDraft.mockResolvedValue({ success: true, data: makeDetail() });
    sharedDraftsApi.getDraftExplanations.mockResolvedValue({
      success: true,
      data: {
        revisionId: 55,
        generatedAt: '2026-09-10T00:00:00.000Z',
        sections: [],
        items: [
          { fieldKey: 'goals-field', rowId: 'row-1', label: 'Improve reading fluency', explanation: 'This goal targets reading speed.' },
          { fieldKey: 'goals-field', rowId: 'row-2', label: 'Improve math skills', explanation: 'This goal targets math accuracy.' },
        ],
        disclaimer: 'AI-generated, not legal advice.',
      },
    });
    renderPage();

    await screen.findByTestId('draft-item-card-goals-field-row-1');
    await user.click(screen.getByTestId('explain-goals-field-row-1'));
    expect(await screen.findByText('This goal targets reading speed.')).toBeInTheDocument();

    await user.click(screen.getByTestId('explain-goals-field-row-2'));
    expect(await screen.findByText('This goal targets math accuracy.')).toBeInTheDocument();

    expect(sharedDraftsApi.getDraftExplanations).toHaveBeenCalledTimes(1);
  });

  it('asks a private question and lists the answer in that card\'s thread', async () => {
    const user = userEvent.setup();
    sharedDraftsApi.getSharedDraft.mockResolvedValue({ success: true, data: makeDetail() });
    const answer: DraftAnswerDto = {
      noteId: 900,
      question: 'Is this ambitious enough?',
      answer: 'The goal appears appropriately ambitious given the baseline.',
      citations: [],
      answeredAt: '2026-09-10T01:00:00.000Z',
      disclaimer: 'AI-generated, not legal advice.',
    };
    sharedDraftsApi.askDraftQuestion.mockResolvedValue({ success: true, data: answer });
    renderPage();

    await user.click(await screen.findByTestId('ask-question-open-goals-field-row-1'));
    expect(await screen.findByTestId('ask-question-drawer-goals-field-row-1')).toHaveTextContent(
      'Private — only you can see this'
    );

    await user.type(screen.getByTestId('ask-question-drawer-goals-field-row-1-input'), 'Is this ambitious enough?');
    await user.click(screen.getByTestId('ask-question-drawer-goals-field-row-1-submit'));

    await waitFor(() =>
      expect(sharedDraftsApi.askDraftQuestion).toHaveBeenCalledWith(55, {
        question: 'Is this ambitious enough?',
        targetFieldKey: 'goals-field',
        targetRowId: 'row-1',
      })
    );
    expect(await screen.findByTestId('ask-question-drawer-goals-field-row-1-thread')).toHaveTextContent(
      'The goal appears appropriately ambitious given the baseline.'
    );
  });

  it('sends a response and shows it in "My responses" with the school reply once resolved', async () => {
    const user = userEvent.setup();
    sharedDraftsApi.getSharedDraft.mockResolvedValue({ success: true, data: makeDetail() });
    const response: DraftResponseDto = {
      id: 700,
      revisionId: 55,
      parentUserId: 1,
      parentName: 'Jamie Parent',
      targetFieldKey: 'goals-field',
      targetRowId: 'row-2',
      targetLabel: 'Improve math skills',
      kind: 'Agree',
      text: 'This works for us.',
      createdAt: '2026-09-10T02:00:00.000Z',
      status: 'Open',
      staffReply: null,
      resolvedInDraft: false,
      resolvedByName: null,
      resolvedAt: null,
    };
    sharedDraftsApi.createDraftResponse.mockResolvedValue({ success: true, data: response });
    renderPage();

    await user.click(await screen.findByTestId('respond-open-goals-field-row-2'));
    await user.type(screen.getByTestId('respond-dialog-goals-field-row-2-text'), 'This works for us.');
    await user.click(screen.getByTestId('respond-dialog-goals-field-row-2-submit'));

    await waitFor(() =>
      expect(sharedDraftsApi.createDraftResponse).toHaveBeenCalledWith(55, {
        kind: 'Agree',
        text: 'This works for us.',
        targetFieldKey: 'goals-field',
        targetRowId: 'row-2',
      })
    );
    expect(await screen.findByTestId('my-responses-section')).toHaveTextContent('This works for us.');
  });

  it('marks the revision reviewed and shows the acknowledgement stamp', async () => {
    const user = userEvent.setup();
    sharedDraftsApi.getSharedDraft.mockResolvedValue({ success: true, data: makeDetail() });
    sharedDraftsApi.acknowledgeSharedDraft.mockResolvedValue({
      success: true,
      data: { ...makeDetail(), acknowledgedAt: '2026-09-11T00:00:00.000Z' },
    });
    renderPage();

    await user.click(await screen.findByTestId('mark-reviewed-button'));

    expect(await screen.findByTestId('draft-acknowledged-stamp')).toHaveTextContent('You reviewed this on');
    expect(screen.queryByTestId('mark-reviewed-button')).not.toBeInTheDocument();
  });

  it('shows the withdrawn banner for a withdrawn revision', async () => {
    sharedDraftsApi.getSharedDraft.mockResolvedValue({
      success: true,
      data: makeDetail({ status: 'Withdrawn', withdrawnAt: '2026-09-12T00:00:00.000Z' }),
    });
    renderPage();

    expect(await screen.findByTestId('revision-withdrawn-banner')).toHaveTextContent('withdrew this shared draft');
  });
});
