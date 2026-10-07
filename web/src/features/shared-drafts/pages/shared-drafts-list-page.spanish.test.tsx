import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { SharedDraftRevisionDto } from '../types';

const sharedDraftsApi = vi.hoisted(() => ({ getSharedDrafts: vi.fn() }));
vi.mock('../api/shared-drafts-api', () => sharedDraftsApi);

import { SharedDraftsListPage } from './shared-drafts-list-page';

function makeRevision(overrides: Partial<SharedDraftRevisionDto> = {}): SharedDraftRevisionDto {
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
    ...overrides,
  };
}

function renderPage() {
  return renderInSpanish(
    <MemoryRouter initialEntries={['/children/3/shared-drafts']}>
      <Routes>
        <Route path="/children/:childId/shared-drafts" element={<SharedDraftsListPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('SharedDraftsListPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the revision list in Spanish', async () => {
    sharedDraftsApi.getSharedDrafts.mockResolvedValue({ success: true, data: [makeRevision()] });

    await renderPage();

    expect(await screen.findByRole('heading', { name: 'Borradores compartidos' })).toBeInTheDocument();
    expect(screen.getByText('IEP · Revisión 2')).toBeInTheDocument();
    expect(screen.getByText(/Compartido el.*por Case Manager/)).toBeInTheDocument();
    expect(screen.getByText('Activa')).toBeInTheDocument();
  });

  it('renders the empty state in Spanish', async () => {
    sharedDraftsApi.getSharedDrafts.mockResolvedValue({ success: true, data: [] });

    await renderPage();

    expect(await screen.findByText('Aún no hay borradores compartidos')).toBeInTheDocument();
  });
});
