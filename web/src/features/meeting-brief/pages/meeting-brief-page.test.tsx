import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { apiRejection } from '@/test/axios-rejection';
import type { MeetingBriefDto } from '../types';

const briefApi = vi.hoisted(() => ({
  getBrief: vi.fn(),
  generateBrief: vi.fn(),
}));
vi.mock('../api/meeting-brief-api', () => briefApi);

import { MeetingBriefPage } from './meeting-brief-page';

function makeBrief(overrides: Partial<MeetingBriefDto> = {}): MeetingBriefDto {
  return {
    meetingId: 42,
    generatedAt: '2026-09-15T00:00:00.000Z',
    source: { kind: 'Draft', id: 7, label: 'Live draft' },
    summary: 'The team proposes increasing speech services to twice weekly.',
    changes: {
      addedRows: [],
      removedRows: [],
      changedRows: [{ fieldKey: 'services', fieldLabel: 'Services', rowId: 'r1', label: 'Speech therapy' }],
      changedFields: [],
      summaryText: 'Speech therapy frequency increased.',
    },
    resourceCommitments: [
      { fieldKey: 'services', rowId: 'r1', label: 'Speech therapy', kind: 'ChangedService', detail: 'Now twice weekly.' },
    ],
    checklist: [
      { key: 'participants', label: 'Required participants present', satisfied: true, detail: null },
      { key: 'notice', label: 'Notice sent 10+ days before', satisfied: false, detail: 'Sent 3 days before.' },
      { key: 'family-input', label: 'Family input received', satisfied: null, detail: null },
    ],
    openFamilyResponses: [
      {
        id: 1,
        revisionId: 3,
        parentUserId: 5,
        parentName: 'Jamie Parent',
        targetFieldKey: null,
        targetRowId: null,
        targetLabel: 'Speech therapy',
        kind: 'Question',
        text: 'Will this affect the school day schedule?',
        createdAt: '2026-09-10T00:00:00.000Z',
        status: 'Open',
        staffReply: null,
        resolvedInDraft: false,
        resolvedByName: null,
        resolvedAt: null,
      },
    ],
    offlineInput: [
      {
        id: 1,
        schoolStudentId: 10,
        documentInstanceId: null,
        receivedAt: '2026-09-05T00:00:00.000Z',
        method: 'Letter',
        summary: 'Family sent a note about scheduling.',
        recordedByUserId: 1,
        recordedByName: 'Casey Manager',
      },
    ],
    contactAttempts: [
      {
        id: 1,
        schoolStudentId: 10,
        attemptedAt: '2026-09-08T00:00:00.000Z',
        method: 'Phone',
        outcome: 'Reached',
        note: null,
        recordedByUserId: 1,
        recordedByName: 'Casey Manager',
      },
    ],
    disclaimer: "Advisory summary — the team's decisions are made in the meeting.",
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/educator/meetings/42/brief']}>
      <Routes>
        <Route path="/educator/meetings/:meetingId/brief" element={<MeetingBriefPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('MeetingBriefPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders every block of the brief', async () => {
    briefApi.getBrief.mockResolvedValue({ success: true, data: makeBrief() });
    renderPage();

    expect(await screen.findByTestId('meeting-brief-page')).toBeInTheDocument();
    expect(screen.getByTestId('brief-source')).toHaveTextContent('Live draft');
    expect(screen.getByTestId('brief-summary')).toHaveTextContent('increasing speech services');
    expect(screen.getByTestId('brief-change-chips')).toBeInTheDocument();
    expect(screen.getByTestId('brief-resource-commitments')).toHaveTextContent('Speech therapy');
    expect(screen.getByTestId('brief-checklist')).toBeInTheDocument();
    expect(screen.getByTestId('checklist-icon-satisfied')).toBeInTheDocument();
    expect(screen.getByTestId('checklist-icon-unsatisfied')).toBeInTheDocument();
    expect(screen.getByTestId('checklist-icon-unknown')).toBeInTheDocument();
    expect(screen.getByTestId('brief-family-responses')).toHaveTextContent('Will this affect the school day schedule?');
    expect(screen.getByTestId('brief-contact-attempts')).toHaveTextContent('Reached');
    expect(screen.getByTestId('brief-offline-input')).toHaveTextContent('Family sent a note about scheduling.');
    expect(screen.getByTestId('brief-disclaimer')).toHaveTextContent('Advisory summary');
  });

  it('regenerates the brief and shows the refreshed summary', async () => {
    const user = userEvent.setup();
    briefApi.getBrief.mockResolvedValue({ success: true, data: makeBrief() });
    briefApi.generateBrief.mockResolvedValue({
      success: true,
      data: makeBrief({ summary: 'Updated: the team proposes daily 1:1 aide support.' }),
    });
    renderPage();

    await screen.findByTestId('brief-summary');
    await user.click(screen.getByTestId('brief-regenerate'));

    await waitFor(() => expect(briefApi.generateBrief).toHaveBeenCalledWith(42));
    expect(await screen.findByTestId('brief-summary')).toHaveTextContent('daily 1:1 aide support');
  });

  it('shows "No brief yet" with a Generate action on a 404', async () => {
    const user = userEvent.setup();
    briefApi.getBrief.mockRejectedValue(apiRejection('Not found', 404));
    briefApi.generateBrief.mockResolvedValue({ success: true, data: makeBrief() });
    renderPage();

    expect(await screen.findByTestId('brief-not-found')).toBeInTheDocument();
    await user.click(screen.getByTestId('brief-generate'));

    await waitFor(() => expect(briefApi.generateBrief).toHaveBeenCalledWith(42));
    expect(await screen.findByTestId('meeting-brief-page')).toBeInTheDocument();
  });
});
