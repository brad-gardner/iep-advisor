import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { User } from '@/types/api';

const authApi = vi.hoisted(() => ({ consumeMagicLink: vi.fn(), requestMagicLink: vi.fn() }));
vi.mock('../api/auth-api', () => authApi);

const applySession = vi.hoisted(() => vi.fn());
vi.mock('../hooks/use-auth', () => ({ useAuth: () => ({ applySession }) }));

import { MagicLinkConsumePage } from './magic-link-consume-page';

function renderPage(search = '?token=abc123') {
  return render(
    <MemoryRouter initialEntries={[`/auth/magic${search}`]}>
      <Routes>
        <Route path="/auth/magic" element={<MagicLinkConsumePage />} />
        <Route path="/dashboard" element={<div data-testid="landed-dashboard" />} />
        <Route path="/mfa-verify" element={<div data-testid="landed-mfa-verify" />} />
        <Route path="/login" element={<div data-testid="landed-login" />} />
      </Routes>
    </MemoryRouter>
  );
}

const user: User = {
  id: 5,
  email: 'staff@example.com',
  firstName: 'Sam',
  lastName: 'Staff',
  state: 'OH',
  role: 'Educator',
  fullName: 'Sam Staff',
  onboardingCompleted: true,
  subscriptionStatus: 'active',
};

describe('MagicLinkConsumePage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('persists the session and routes to /dashboard on a plain success', async () => {
    authApi.consumeMagicLink.mockResolvedValue({ success: true, data: { token: 'jwt', user } });
    renderPage();

    await waitFor(() => expect(applySession).toHaveBeenCalledWith('jwt', user));
    expect(await screen.findByTestId('landed-dashboard')).toBeInTheDocument();
    expect(authApi.consumeMagicLink).toHaveBeenCalledWith('abc123');
  });

  it('shows a guidance message (no session, no redirect) when the district requires MFA setup before magic-link works', async () => {
    const userEv = userEvent.setup();
    authApi.consumeMagicLink.mockResolvedValue({
      success: true,
      message: 'This district requires multi-factor authentication. Please sign in with your password to finish setting it up.',
      data: { requiresMfa: true, mfaSetupRequired: true },
    });
    renderPage();

    expect(await screen.findByTestId('magic-consume-mfa-setup')).toHaveTextContent(
      'Please sign in with your password to finish setting it up.'
    );
    expect(applySession).not.toHaveBeenCalled();
    expect(screen.queryByTestId('landed-dashboard')).not.toBeInTheDocument();

    await userEv.click(screen.getByTestId('magic-consume-go-password'));
    expect(await screen.findByTestId('landed-login')).toBeInTheDocument();
  });

  it('routes to /mfa-verify with the pending token when the user already has MFA enrolled, without persisting a session', async () => {
    authApi.consumeMagicLink.mockResolvedValue({
      success: true,
      data: { requiresMfa: true, mfaPendingToken: 'pending-1' },
    });
    renderPage();

    expect(await screen.findByTestId('landed-mfa-verify')).toBeInTheDocument();
    expect(applySession).not.toHaveBeenCalled();
  });

  it('shows an invalid-link error and lets the user request a new one', async () => {
    const userEv = userEvent.setup();
    authApi.consumeMagicLink.mockResolvedValue({ success: false, message: 'Invalid or expired token' });
    renderPage();

    expect(await screen.findByTestId('magic-consume-error')).toHaveTextContent('This link is invalid or has expired');
    expect(screen.queryByTestId('magic-link-form')).not.toBeInTheDocument();

    await userEv.click(screen.getByTestId('magic-consume-request-new'));
    expect(screen.getByTestId('magic-link-form')).toBeInTheDocument();
  });

  it('shows the invalid-link error immediately when the URL has no token, without calling the API', async () => {
    renderPage('');
    expect(await screen.findByTestId('magic-consume-error')).toBeInTheDocument();
    expect(authApi.consumeMagicLink).not.toHaveBeenCalled();
  });

  it('shows the invalid-link error when the request throws', async () => {
    authApi.consumeMagicLink.mockRejectedValue(new Error('network down'));
    renderPage();
    expect(await screen.findByTestId('magic-consume-error')).toBeInTheDocument();
  });
});
