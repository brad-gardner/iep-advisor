import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const api = vi.hoisted(() => ({ listAllForUser: vi.fn() }));
vi.mock('../api/etr-documents-api', () => api);

import { EtrListPage } from './etr-list-page';

describe('EtrListPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the title and empty state in Spanish', async () => {
    api.listAllForUser.mockResolvedValue({ success: true, data: [] });

    await renderInSpanish(
      <MemoryRouter>
        <EtrListPage />
      </MemoryRouter>,
      { ns: 'etr-documents' },
    );

    expect(await screen.findByRole('heading', { name: 'Evaluaciones (ETR)' })).toBeInTheDocument();
    expect(screen.getByText('Aún no hay evaluaciones.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Ir a Mis hijos/ })).toBeInTheDocument();
  });
});
