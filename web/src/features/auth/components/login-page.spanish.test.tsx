import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const login = vi.hoisted(() => vi.fn());
vi.mock('../hooks/use-auth', () => ({ useAuth: () => ({ login }) }));
const api = vi.hoisted(() => ({ requestMagicLink: vi.fn().mockResolvedValue({ success: true }) }));
vi.mock('../api/auth-api', () => api);

import { LoginPage } from './login-page';

describe('LoginPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading, field labels, and primary action in Spanish', async () => {
    await renderInSpanish(
      <MemoryRouter initialEntries={['/login']}>
        <LoginPage />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: 'Le damos la bienvenida' })).toBeInTheDocument();
    expect(screen.getByLabelText('Correo electrónico')).toBeInTheDocument();
    expect(screen.getByLabelText('Contraseña')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Iniciar sesión' })).toBeInTheDocument();
    expect(screen.getByText('¿Olvidó su contraseña?')).toBeInTheDocument();
    expect(screen.getByText('¿No tiene una cuenta?')).toBeInTheDocument();
  });
});
