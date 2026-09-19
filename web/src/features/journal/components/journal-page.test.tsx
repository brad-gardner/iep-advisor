import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom';
import type { ChildProfile } from '@/types/api';

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
vi.mock('@/components/ui/toast', () => ({ useToast: () => ({ show: vi.fn() }) }));

import { JournalPage } from './journal-page';
import type { JournalEntryDto } from '../types/journal';

const child = (role: ChildProfile['role']): ChildProfile => ({
  id: 4,
  firstName: 'Jordan',
  lastName: 'Lee',
  dateOfBirth: null,
  gradeLevel: null,
  disabilityCategory: null,
  schoolDistrict: null,
  role,
  currentIepDocumentId: null,
  createdAt: '2026-01-01',
  updatedAt: '2026-01-01',
});

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

/** Mounts the page the way the app does: as an Outlet child of the child layout. */
function renderPage(role: ChildProfile['role'] = 'owner', url = '/children/4/journal') {
  const ctx = { child: child(role), childId: 4, reloadChild: () => Promise.resolve() };
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/children/:childId" element={<Outlet context={ctx} />}>
          <Route path="journal" element={<JournalPage />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe('JournalPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.listJournalEntries.mockResolvedValue({
      success: true,
      data: [entry(2, { occurredOn: '2026-09-12', tag: 'Medical' }), entry(1, { occurredOn: '2026-09-03' })],
    });
  });

  it('lists every entry and re-queries with ?tag= when the filter changes', async () => {
    renderPage();
    const list = await screen.findByTestId('journal-list');
    expect(within(list).getAllByRole('listitem')).toHaveLength(2);
    expect(api.listJournalEntries).toHaveBeenLastCalledWith(4, { tag: undefined, take: 200 });

    api.listJournalEntries.mockResolvedValueOnce({
      success: true,
      data: [entry(2, { occurredOn: '2026-09-12', tag: 'Medical' })],
    });
    fireEvent.change(screen.getByLabelText('Show'), { target: { value: 'Medical' } });
    await waitFor(() => expect(api.listJournalEntries).toHaveBeenLastCalledWith(4, { tag: 'Medical', take: 200 }));
    await waitFor(() => expect(within(screen.getByTestId('journal-list')).getAllByRole('listitem')).toHaveLength(1));

    fireEvent.change(screen.getByLabelText('Show'), { target: { value: '' } });
    await waitFor(() => expect(api.listJournalEntries).toHaveBeenLastCalledWith(4, { tag: undefined, take: 200 }));
  });

  it('shows a kind-specific empty state when a filter matches nothing', async () => {
    api.listJournalEntries.mockResolvedValue({ success: true, data: [] });
    renderPage();
    await screen.findByTestId('journal-empty');
    fireEvent.change(screen.getByLabelText('Show'), { target: { value: 'Incident' } });
    await waitFor(() => expect(screen.getByTestId('journal-empty')).toHaveTextContent('No incident updates yet'));
    expect(screen.getByTestId('journal-empty')).toHaveTextContent('Private to your family.');
  });

  it('opens the shared drawer to add and to edit from the page', async () => {
    api.createJournalEntry.mockResolvedValue({ success: true, data: entry(3) });
    renderPage();
    await screen.findByTestId('journal-list');

    fireEvent.click(screen.getByTestId('journal-add'));
    const form = await screen.findByTestId('journal-entry-drawer-form');
    fireEvent.change(within(form).getByLabelText('What happened'), { target: { value: 'Called the school.' } });
    fireEvent.change(within(form).getByLabelText('Kind of update'), { target: { value: 'Communication' } });
    fireEvent.click(screen.getByTestId('journal-entry-drawer-save'));
    await waitFor(() =>
      expect(api.createJournalEntry).toHaveBeenCalledWith(
        4,
        expect.objectContaining({ tag: 'Communication', contentMarkdown: 'Called the school.' }),
      ),
    );
    await waitFor(() => expect(api.listJournalEntries).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.queryByTestId('journal-entry-drawer-form')).not.toBeInTheDocument());

    fireEvent.click(screen.getByTestId('journal-entry-1-edit'));
    expect(await screen.findByRole('heading', { name: 'Edit update' })).toBeInTheDocument();
  });

  it('marks and scrolls to the entry named in ?entry= (an advocate citation)', async () => {
    const scrollIntoView = vi.fn();
    Element.prototype.scrollIntoView = scrollIntoView;
    renderPage('owner', '/children/4/journal?entry=1');
    await screen.findByTestId('journal-list');
    const target = screen.getByTestId('journal-entry-1');
    expect(target).toHaveAttribute('id', 'journal-entry-1');
    expect(target).toHaveAttribute('data-highlighted', 'true');
    expect(screen.getByTestId('journal-entry-2')).not.toHaveAttribute('data-highlighted');
    await waitFor(() => expect(scrollIntoView).toHaveBeenCalled());
    expect(scrollIntoView.mock.instances[0]).toBe(target);
  });

  it('gives each entry an "Ask the advocate" launcher about that entry', async () => {
    renderPage();
    await screen.findByTestId('journal-list');
    const launcher = screen.getByTestId('journal-entry-2-ask');
    expect(launcher).toHaveAttribute('href', '/children/4/advocate?about=journal%3A2');
    expect(launcher).toHaveAccessibleName('Ask the advocate about the update from Sep 12, 2026');
  });

  it('hides every write control from a viewer', async () => {
    renderPage('viewer');
    await screen.findByTestId('journal-list');
    expect(screen.queryByTestId('journal-add')).not.toBeInTheDocument();
    expect(screen.queryByTestId('journal-entry-1-edit')).not.toBeInTheDocument();
    expect(screen.queryByTestId('journal-entry-1-ask')).not.toBeInTheDocument();
    expect(screen.queryByTestId('journal-entry-drawer')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Show')).toBeInTheDocument(); // reading and filtering still work
  });
});
