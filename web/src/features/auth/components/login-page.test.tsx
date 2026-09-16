import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

const login = vi.hoisted(() => vi.fn());
vi.mock('../hooks/use-auth', () => ({ useAuth: () => ({ login }) }));
const api = vi.hoisted(() => ({ requestMagicLink: vi.fn().mockResolvedValue({ success: true }) }));
vi.mock('../api/auth-api', () => api);

import { LoginPage } from './login-page';

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <LoginPage />
    </MemoryRouter>
  );
}

describe('LoginPage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('moves focus to the panel that appears when toggling between password and magic-link sign-in', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByTestId('magic-link-toggle'));
    expect(document.activeElement).toBe(screen.getByTestId('login-magic-link-panel'));
    await user.click(screen.getByTestId('magic-link-back'));
    expect(document.activeElement).toBe(screen.getByTestId('login-password-panel'));
  });

  it('drops a password-login failure that lands after the user moved to the magic-link panel, and never leaves Sign In stuck disabled', async () => {
    const user = userEvent.setup();
    let finishLogin!: (v: unknown) => void;
    login.mockReturnValueOnce(new Promise((resolve) => (finishLogin = resolve)));
    renderPage();

    await user.type(screen.getByLabelText(/email/i), 'pat@example.com');
    await user.type(screen.getByLabelText(/password/i), 'wrong');
    await user.click(screen.getByRole('button', { name: /sign in/i }));
    await user.click(screen.getByTestId('magic-link-toggle')); // abandon the in-flight login

    await act(async () => finishLogin({ success: false, error: 'Invalid email or password' }));
    expect(screen.queryByTestId('login-error')).not.toBeInTheDocument(); // stale failure dropped

    await user.click(screen.getByTestId('magic-link-back'));
    await waitFor(() => expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled()); // loading reset on every path
  });
});
