import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom';
import type { ChildProfile, User } from '@/types/api';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const api = vi.hoisted(() => ({
  listAdvocateThreads: vi.fn(),
  createAdvocateThread: vi.fn(),
  getAdvocateThread: vi.fn(),
  renameAdvocateThread: vi.fn(),
  deleteAdvocateThread: vi.fn(),
  getAdvocateUsage: vi.fn(),
  getAdvocateChildContext: vi.fn(),
  streamAdvocateMessage: vi.fn(),
}));
vi.mock('../api/advocate-api', async () => {
  const actual = await vi.importActual<typeof import('../api/advocate-api')>('../api/advocate-api');
  return { ...actual, ...api };
});
vi.mock('@/components/ui/toast', () => ({ useToast: () => ({ show: vi.fn() }) }));
vi.mock('@/features/journal/hooks/use-journal-link-options', () => ({
  useJournalLinkOptions: () => ({ options: { ieps: [], etrs: [], meetings: [] }, loading: false }),
}));
const journalApi = vi.hoisted(() => ({
  listJournalEntries: vi.fn(),
  createJournalEntry: vi.fn(),
  updateJournalEntry: vi.fn(),
  deleteJournalEntry: vi.fn(),
}));
vi.mock('@/features/journal/api/journal-api', () => journalApi);
const auth = vi.hoisted(() => ({ user: null as User | null }));
vi.mock('@/features/auth/hooks/use-auth', () => ({ useAuth: () => ({ user: auth.user }) }));

import { AdvocatePage } from './advocate-page';

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

function renderPage(role: ChildProfile['role'] = 'owner') {
  const ctx = { child: child(role), childId: 4, reloadChild: () => Promise.resolve() };
  return renderInSpanish(
    <MemoryRouter initialEntries={['/children/4/advocate']}>
      <Routes>
        <Route path="/children/:childId" element={<Outlet context={ctx} />}>
          <Route path="advocate" element={<AdvocatePage />} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe('AdvocatePage in Spanish', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    auth.user = null;
    journalApi.listJournalEntries.mockResolvedValue({ success: true, data: [] });
    api.listAdvocateThreads.mockResolvedValue({ success: true, data: [] });
    api.getAdvocateUsage.mockResolvedValue({ success: true, data: { used: 0, limit: 300, subscriptionActive: true } });
    api.getAdvocateChildContext.mockResolvedValue({ success: true, data: { stateCode: 'OH' } });
  });
  afterEach(() => resetTestLanguage());

  it('renders the heading, privacy banner, composer and example questions in Spanish', async () => {
    await renderPage();

    expect(await screen.findByRole('heading', { name: 'Pregúntele al asesor virtual sobre Jordan' })).toBeInTheDocument();
    expect(screen.getByTestId('advocate-privacy-banner')).toHaveTextContent('solo usted puede ver esto');
    expect(screen.getByRole('button', { name: 'Enviar' })).toBeInTheDocument();
    const examples = screen.getAllByTestId('advocate-example');
    expect(examples[0]).toHaveTextContent('¿Qué es el aviso previo por escrito?');
  });

  it('renders the viewer notice in Spanish for a read-only collaborator', async () => {
    await renderPage('viewer');

    expect(await screen.findByTestId('advocate-viewer-notice')).toHaveTextContent(
      'Puede ver a este hijo o hija, pero no puede preguntarle al asesor virtual sobre él o ella.'
    );
  });
});
