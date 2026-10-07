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
});
