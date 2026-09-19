import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

const api = vi.hoisted(() => ({
  listJournalEntries: vi.fn(),
  createJournalEntry: vi.fn(),
  updateJournalEntry: vi.fn(),
  deleteJournalEntry: vi.fn(),
}));
vi.mock('../api/journal-api', () => api);
vi.mock('../hooks/use-journal-link-options', () => ({
  useJournalLinkOptions: () => ({ options: { ieps: [], etrs: [], meetings: [] }, loading: false }),
}));
const toast = vi.hoisted(() => ({ show: vi.fn() }));
vi.mock('@/components/ui/toast', () => ({ useToast: () => toast }));

import { JournalCard } from './journal-card';
import type { JournalEntryDto } from '../types/journal';

const entry = (id: number, overrides: Partial<JournalEntryDto> = {}): JournalEntryDto => ({
  id,
  childProfileId: 4,
  occurredOn: '2026-09-10',
  tag: 'Other',
  contentMarkdown: `entry ${id}`,
  createdAt: '2026-09-10T12:00:00Z',
  updatedAt: '2026-09-10T12:00:00Z',
  createdById: 1,
  ...overrides,
});

function renderCard(canEdit = true) {
  return render(
    <MemoryRouter>
      <JournalCard childId={4} childName="Jordan" canEdit={canEdit} />
    </MemoryRouter>,
  );
}

describe('JournalCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.listJournalEntries.mockResolvedValue({
      success: true,
      data: [
        entry(2, { occurredOn: '2026-09-12', tag: 'Incident', contentMarkdown: 'Sent home **early** today.' }),
        entry(1, { occurredOn: '2026-09-03', tag: 'Progress', contentMarkdown: 'Read a whole chapter alone.' }),
      ],
    });
  });

  it('lists the recent entries in the order the server gives (newest day first) with a date, tag chip and rendered markdown', async () => {
    renderCard();
    const list = await screen.findByTestId('journal-recent');
    const rows = within(list).getAllByRole('listitem');
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveAttribute('data-testid', 'journal-entry-2');
    expect(rows[1]).toHaveAttribute('data-testid', 'journal-entry-1');

    expect(within(rows[0]).getByText('Sep 12, 2026')).toBeInTheDocument();
    expect(screen.getByTestId('journal-entry-2-tag')).toHaveTextContent('Incident');
    expect(screen.getByTestId('journal-entry-1-tag')).toHaveTextContent('Progress');
    expect(screen.getByTestId('journal-entry-2-content').querySelector('strong')).toHaveTextContent('early');

    expect(api.listJournalEntries).toHaveBeenCalledWith(4, { take: 5 });
    expect(screen.getByTestId('journal-see-all')).toHaveAttribute('href', '/children/4/journal');
  });

  it('explains what the journal is for when there are no entries', async () => {
    api.listJournalEntries.mockResolvedValue({ success: true, data: [] });
    renderCard();
    const empty = await screen.findByTestId('journal-empty');
    expect(empty).toHaveTextContent('Keep a dated record of what happens');
    expect(empty).toHaveTextContent('Private to your family.');
    expect(screen.queryByTestId('journal-recent')).not.toBeInTheDocument();
  });

  it('"Add update" opens the drawer; saving posts the date, tag and markdown, then re-reads the list', async () => {
    api.createJournalEntry.mockResolvedValue({ success: true, data: entry(3, { occurredOn: '2026-09-15', tag: 'Medical' }) });
    renderCard();
    await screen.findByTestId('journal-recent');
    expect(screen.queryByTestId('journal-entry-drawer-form')).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('journal-add'));
    const form = await screen.findByTestId('journal-entry-drawer-form');
    expect(screen.getByRole('heading', { name: 'Add an update' })).toBeInTheDocument();

    const date = within(form).getByLabelText('Date') as HTMLInputElement;
    expect(date.value).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(date).toHaveAttribute('max', date.value);
    expect(screen.getByTestId('journal-entry-drawer-save')).toBeDisabled(); // nothing written yet

    fireEvent.change(date, { target: { value: '2026-09-15' } });
    fireEvent.change(within(form).getByLabelText('Kind of update'), { target: { value: 'Medical' } });
    fireEvent.change(within(form).getByLabelText('What happened'), { target: { value: 'New **inhaler** prescribed.' } });
    fireEvent.click(screen.getByTestId('journal-entry-drawer-save'));

    await waitFor(() =>
      expect(api.createJournalEntry).toHaveBeenCalledWith(
        4,
        expect.objectContaining({ occurredOn: '2026-09-15', tag: 'Medical', contentMarkdown: 'New **inhaler** prescribed.' }),
      ),
    );
    await waitFor(() => expect(api.listJournalEntries).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.queryByTestId('journal-entry-drawer-form')).not.toBeInTheDocument());
    expect(toast.show).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' }));
  });

  it('editing pre-fills the drawer and PUTs the changed entry', async () => {
    api.updateJournalEntry.mockResolvedValue({ success: true, data: entry(1, { contentMarkdown: 'Read two chapters alone.' }) });
    renderCard();
    await screen.findByTestId('journal-recent');

    fireEvent.click(screen.getByTestId('journal-entry-1-edit'));
    const form = await screen.findByTestId('journal-entry-drawer-form');
    expect(screen.getByRole('heading', { name: 'Edit update' })).toBeInTheDocument();
    expect(within(form).getByLabelText('Date')).toHaveValue('2026-09-03');
    expect(within(form).getByLabelText('Kind of update')).toHaveValue('Progress');
    expect(within(form).getByLabelText('What happened')).toHaveValue('Read a whole chapter alone.');

    fireEvent.change(within(form).getByLabelText('What happened'), { target: { value: 'Read two chapters alone.' } });
    fireEvent.click(screen.getByTestId('journal-entry-drawer-save'));

    await waitFor(() =>
      expect(api.updateJournalEntry).toHaveBeenCalledWith(
        1,
        expect.objectContaining({ occurredOn: '2026-09-03', tag: 'Progress', contentMarkdown: 'Read two chapters alone.' }),
      ),
    );
    expect(api.createJournalEntry).not.toHaveBeenCalled();
    await waitFor(() => expect(api.listJournalEntries).toHaveBeenCalledTimes(2));
  });

  it('deleting asks for confirmation before the DELETE, then re-reads the list', async () => {
    api.deleteJournalEntry.mockResolvedValue({ success: true });
    renderCard();
    await screen.findByTestId('journal-recent');

    fireEvent.click(screen.getByTestId('journal-entry-2-edit'));
    await screen.findByTestId('journal-entry-drawer-form');
    fireEvent.click(screen.getByTestId('journal-entry-drawer-delete'));
    expect(api.deleteJournalEntry).not.toHaveBeenCalled();

    const dialog = await screen.findByRole('alertdialog');
    expect(dialog).toHaveTextContent('Delete this journal update?');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));

    await waitFor(() => expect(api.deleteJournalEntry).toHaveBeenCalledWith(2));
    await waitFor(() => expect(api.listJournalEntries).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.queryByTestId('journal-entry-drawer-form')).not.toBeInTheDocument());
  });

  it('keeps a failed delete inside the confirmation dialog with the server message', async () => {
    api.deleteJournalEntry.mockResolvedValue({ success: false, message: 'Entry not found' });
    renderCard();
    await screen.findByTestId('journal-recent');

    fireEvent.click(screen.getByTestId('journal-entry-2-edit'));
    await screen.findByTestId('journal-entry-drawer-form');
    fireEvent.click(screen.getByTestId('journal-entry-drawer-delete'));
    const dialog = await screen.findByRole('alertdialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));

    await waitFor(() => expect(screen.getByRole('alertdialog')).toHaveTextContent('Entry not found'));
    expect(api.listJournalEntries).toHaveBeenCalledTimes(1);
  });

  it('blocks submit while the markdown is over the 4000-character limit', async () => {
    renderCard();
    await screen.findByTestId('journal-recent');
    fireEvent.click(screen.getByTestId('journal-add'));
    const form = await screen.findByTestId('journal-entry-drawer-form');

    fireEvent.change(within(form).getByLabelText('What happened'), { target: { value: 'x'.repeat(4001) } });
    expect(screen.getByTestId('journal-entry-drawer-save')).toBeDisabled();
    fireEvent.submit(form);
    expect(api.createJournalEntry).not.toHaveBeenCalled();

    fireEvent.change(within(form).getByLabelText('What happened'), { target: { value: 'x'.repeat(4000) } });
    expect(screen.getByTestId('journal-entry-drawer-save')).toBeEnabled();
  });

  it('blocks a future date', async () => {
    renderCard();
    await screen.findByTestId('journal-recent');
    fireEvent.click(screen.getByTestId('journal-add'));
    const form = await screen.findByTestId('journal-entry-drawer-form');
    fireEvent.change(within(form).getByLabelText('What happened'), { target: { value: 'Tomorrow never comes' } });
    fireEvent.change(within(form).getByLabelText('Date'), { target: { value: '2999-01-01' } });
    expect(screen.getByTestId('journal-entry-drawer-date-error')).toBeInTheDocument();
    expect(screen.getByTestId('journal-entry-drawer-save')).toBeDisabled();
  });

  it('shows the server message when a save is refused', async () => {
    api.createJournalEntry.mockResolvedValue({ success: false, message: 'Content exceeds 4000 characters.' });
    renderCard();
    await screen.findByTestId('journal-recent');
    fireEvent.click(screen.getByTestId('journal-add'));
    const form = await screen.findByTestId('journal-entry-drawer-form');
    fireEvent.change(within(form).getByLabelText('What happened'), { target: { value: 'hello' } });
    fireEvent.click(screen.getByTestId('journal-entry-drawer-save'));

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Content exceeds 4000 characters.'));
    expect(screen.getByTestId('journal-entry-drawer-form')).toBeInTheDocument(); // still open to fix and retry
    expect(api.listJournalEntries).toHaveBeenCalledTimes(1);
  });

  it('is read-only for viewers: no add, no edit, no drawer', async () => {
    renderCard(false);
    await screen.findByTestId('journal-recent');
    expect(screen.queryByTestId('journal-add')).not.toBeInTheDocument();
    expect(screen.queryByTestId('journal-entry-2-edit')).not.toBeInTheDocument();
    expect(screen.queryByTestId('journal-entry-drawer')).not.toBeInTheDocument();
    expect(screen.getByTestId('journal-see-all')).toBeInTheDocument();
  });

  it('surfaces a load failure with a retry', async () => {
    api.listJournalEntries
      .mockResolvedValueOnce({ success: false, message: 'Child not found' })
      .mockResolvedValueOnce({ success: true, data: [entry(1)] });
    renderCard();
    await screen.findByText('Child not found');
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await screen.findByTestId('journal-entry-1');
  });
});
