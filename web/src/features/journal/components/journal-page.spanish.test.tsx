import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom';
import type { ChildProfile } from '@/types/api';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

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

function renderPage() {
  const ctx = { child: child('owner'), childId: 4, reloadChild: () => Promise.resolve() };
  return renderInSpanish(
    <MemoryRouter initialEntries={['/children/4/journal']}>
      <Routes>
        <Route path="/children/:childId" element={<Outlet context={ctx} />}>
          <Route path="journal" element={<JournalPage />} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe('JournalPage in Spanish', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.listJournalEntries.mockResolvedValue({
      success: true,
      data: [entry(1, { tag: 'Incident', contentMarkdown: 'Se envió a casa temprano.' })],
    });
  });
  afterEach(() => resetTestLanguage());

  it('renders the heading, filter and tag chip in Spanish', async () => {
    await renderPage();

    expect(await screen.findByRole('heading', { name: 'Diario de Jordan' })).toBeInTheDocument();
    expect(screen.getByLabelText('Mostrar')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Agregar actualización/ })).toBeInTheDocument();
    const list = screen.getByTestId('journal-list');
    expect(list).toHaveTextContent('Incidente');
  });

  it('renders the empty state in Spanish', async () => {
    api.listJournalEntries.mockResolvedValue({ success: true, data: [] });
    await renderPage();

    expect(await screen.findByText('Aún no hay nada en el diario')).toBeInTheDocument();
  });
});
