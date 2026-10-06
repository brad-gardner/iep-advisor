import { afterEach, describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { RegisterPage } from './register-page';

describe('RegisterPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading and both account-type choices in Spanish', async () => {
    await renderInSpanish(
      <MemoryRouter initialEntries={['/register']}>
        <RegisterPage />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: 'Crear su cuenta' })).toBeInTheDocument();
    expect(screen.getByText('Soy padre, madre o tutor')).toBeInTheDocument();
    expect(screen.getByText('Represento a una escuela o distrito')).toBeInTheDocument();
    expect(screen.getByText('¿Ya tiene una cuenta?')).toBeInTheDocument();
  });
});
