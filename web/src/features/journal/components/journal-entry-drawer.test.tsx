import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const api = vi.hoisted(() => ({
  createJournalEntry: vi.fn(),
  updateJournalEntry: vi.fn(),
  deleteJournalEntry: vi.fn(),
}));
vi.mock('../api/journal-api', () => api);
const docs = vi.hoisted(() => ({
  getIepDocuments: vi.fn(),
  listByChild: vi.fn(),
  listChildMeetings: vi.fn(),
}));
vi.mock('@/features/iep-documents/api/iep-documents-api', () => ({ getIepDocuments: docs.getIepDocuments }));
vi.mock('@/features/etr-documents/api/etr-documents-api', () => ({ listByChild: docs.listByChild }));
vi.mock('@/features/meetings/api/meetings-api', () => ({ listChildMeetings: docs.listChildMeetings }));

import { JournalEntryDrawer } from './journal-entry-drawer';
import type { JournalEntryDto } from '../types/journal';

const iep = {
  id: 11,
  childProfileId: 4,
  fileName: 'iep.pdf',
  uploadDate: '2026-01-01',
  iepDate: '2026-01-15',
  status: 'Ready',
  fileSizeBytes: 1,
  meetingType: null,
  attendees: null,
  notes: null,
  createdAt: '2026-01-01',
};
const etr = {
  id: 21,
  childProfileId: 4,
  fileName: 'etr.pdf',
  uploadDate: '2026-01-01',
  evaluationDate: '2025-11-02',
  evaluationType: 'Reevaluation',
  documentState: 'Final',
  notes: null,
  status: 'Ready',
  fileSizeBytes: 1,
  createdAt: '2026-01-01',
};
const meeting = { id: 31, title: 'Annual review', startsAtUtc: '2026-03-04T15:00:00Z' };

const existing: JournalEntryDto = {
  id: 7,
  childProfileId: 4,
  occurredOn: '2026-09-01',
  tag: 'Communication',
  contentMarkdown: 'Emailed the case manager.',
  linkedIepDocumentId: 11,
  createdAt: '2026-09-01T00:00:00Z',
  updatedAt: '2026-09-01T00:00:00Z',
  createdById: 1,
};

describe('JournalEntryDrawer link selectors', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    docs.getIepDocuments.mockResolvedValue({ success: true, data: [iep] });
    docs.listByChild.mockResolvedValue({ success: true, data: [etr] });
    docs.listChildMeetings.mockResolvedValue({ success: true, data: [meeting] });
    api.createJournalEntry.mockResolvedValue({ success: true, data: { ...existing, id: 8 } });
    api.updateJournalEntry.mockResolvedValue({ success: true, data: existing });
  });

  it('offers the child IEPs, ETRs and meetings and sends the chosen ids', async () => {
    render(<JournalEntryDrawer open onClose={() => {}} childId={4} onSaved={() => {}} />);
    const iepSelect = await screen.findByLabelText('IEP');
    expect(iepSelect).toHaveTextContent('IEP · Jan 15, 2026');
    expect(screen.getByLabelText('ETR')).toHaveTextContent('Reevaluation · Nov 2, 2025');
    expect(screen.getByLabelText('Meeting')).toHaveTextContent('Annual review');

    fireEvent.change(screen.getByLabelText('What happened'), { target: { value: 'Progress note' } });
    fireEvent.change(iepSelect, { target: { value: '11' } });
    fireEvent.change(screen.getByLabelText('Meeting'), { target: { value: '31' } });
    fireEvent.click(screen.getByTestId('journal-entry-drawer-save'));

    await waitFor(() =>
      expect(api.createJournalEntry).toHaveBeenCalledWith(
        4,
        expect.objectContaining({ linkedIepDocumentId: 11, linkedEtrDocumentId: null, linkedMeetingId: 31 }),
      ),
    );
  });

  it('omits the meeting selector for a child with no meetings (not school-linked) and hides lists that fail to load', async () => {
    docs.listChildMeetings.mockResolvedValue({ success: true, data: [] });
    docs.listByChild.mockRejectedValue(new Error('403'));
    render(<JournalEntryDrawer open onClose={() => {}} childId={4} onSaved={() => {}} />);
    await screen.findByLabelText('IEP');
    expect(screen.queryByLabelText('Meeting')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('ETR')).not.toBeInTheDocument();
  });

  it('shows no link section at all when there is nothing to link to', async () => {
    docs.getIepDocuments.mockResolvedValue({ success: true, data: [] });
    docs.listByChild.mockResolvedValue({ success: true, data: [] });
    docs.listChildMeetings.mockResolvedValue({ success: false, message: 'Forbidden' });
    render(<JournalEntryDrawer open onClose={() => {}} childId={4} onSaved={() => {}} />);
    await waitFor(() => expect(docs.getIepDocuments).toHaveBeenCalled());
    await waitFor(() => expect(screen.queryByText('Link to (optional)')).not.toBeInTheDocument());
    expect(screen.getByLabelText('What happened')).toBeInTheDocument();
  });

  it('pre-selects an existing link and keeps one the lists no longer contain', async () => {
    docs.getIepDocuments.mockResolvedValue({ success: true, data: [] }); // the linked IEP is gone
    const onSaved = vi.fn();
    render(
      <JournalEntryDrawer
        open
        onClose={() => {}}
        childId={4}
        entry={{ ...existing, linkedEtrDocumentId: 21 }}
        onSaved={onSaved}
      />,
    );
    const etrSelect = await screen.findByLabelText('ETR');
    expect(etrSelect).toHaveValue('21');
    expect(screen.queryByLabelText('IEP')).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('journal-entry-drawer-save'));
    await waitFor(() =>
      expect(api.updateJournalEntry).toHaveBeenCalledWith(
        7,
        expect.objectContaining({ linkedIepDocumentId: 11, linkedEtrDocumentId: 21 }),
      ),
    );
    expect(onSaved).toHaveBeenCalledWith(existing, 'updated');
  });
});
