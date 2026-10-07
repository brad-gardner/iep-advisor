import { afterEach, describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { Iep101Page } from './iep-101-page';

describe('Iep101Page in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders headings and glossary terms in Spanish', async () => {
    await renderInSpanish(
      <MemoryRouter>
        <Iep101Page />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: 'IEP 101', level: 1 })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '¿Qué es un IEP?' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Sus derechos como padre, madre o tutor' })).toBeInTheDocument();
    expect(screen.getByText('Explore nuestro centro de recursos completo')).toBeInTheDocument();
    expect(screen.getByText('Ambiente Menos Restrictivo')).toBeInTheDocument();
  });

  // Phase 2 review: glossary-consistent wording for present levels, annual
  // goals, and due process (both the glossary term and its mention under
  // "Your Rights").
  it('uses the established Spanish special-education terms', async () => {
    await renderInSpanish(
      <MemoryRouter>
        <Iep101Page />
      </MemoryRouter>
    );

    expect(screen.getByText('Niveles actuales de desempeño:')).toBeInTheDocument();
    expect(
      screen.getByText('Metas medibles para el año (deben ser específicas, medibles, alcanzables, relevantes y con plazos definidos)')
    ).toBeInTheDocument();
    expect(screen.getByText('Debido proceso')).toBeInTheDocument();
    expect(screen.getByText('Una audiencia de debido proceso formal para resolver desacuerdos')).toBeInTheDocument();
    expect(
      screen.getByText('Usted puede estar en desacuerdo, y existen procesos formales para resolver disputas (mediación, debido proceso)')
    ).toBeInTheDocument();
  });
});
