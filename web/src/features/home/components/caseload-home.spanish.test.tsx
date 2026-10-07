import { afterEach, describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { makeHomeDraft, makeStaffHome } from '../test/fixtures';
import { CaseloadHome } from './caseload-home';

function renderHome(overrides: Parameters<typeof makeStaffHome>[0] = {}) {
  return renderInSpanish(
    <MemoryRouter>
      <CaseloadHome staff={makeStaffHome(overrides)} />
    </MemoryRouter>
  );
}

describe('CaseloadHome in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('translates section headings, empty hints, and the completeness badge', async () => {
    await renderHome({
      drafts: [makeHomeDraft({ instanceId: 200, studentId: 12, studentName: 'Cleo Curie' })],
    });

    expect(screen.getByText('Esta semana')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Por vencer o vencido' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Borradores en progreso' })).toBeInTheDocument();
    expect(screen.getByTestId('home-due-soon-empty')).toHaveTextContent(
      'Nada por vencer o vencido en su lista de casos.'
    );
    expect(screen.getByTestId('home-drafts-200')).toHaveTextContent('62% completo');
  });

  it('translates "Provider requests I owe" for the Provider variant', async () => {
    await renderHome({ variant: 'Provider' });

    expect(screen.getByRole('heading', { name: 'Solicitudes de proveedores pendientes' })).toBeInTheDocument();
  });
});
