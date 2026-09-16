import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';

const authApi = vi.hoisted(() => ({ cancelDeletionByToken: vi.fn() }));
vi.mock('../api/auth-api', () => authApi);

import { CancelDeletionPage } from './cancel-deletion-page';

function renderPage(search = '?token=abc123') {
  return render(
    <MemoryRouter initialEntries={[`/account/cancel-deletion${search}`]}>
      <Routes>
        <Route path="/account/cancel-deletion" element={<CancelDeletionPage />} />
        <Route path="/login" element={<div data-testid="landed-login" />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('CancelDeletionPage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('shows a success message once the token is confirmed', async () => {
    authApi.cancelDeletionByToken.mockResolvedValue({ success: true, data: null, message: 'Cancelled' });
    renderPage();

    expect(await screen.findByTestId('cancel-deletion-success')).toHaveTextContent(
      'Your deletion request was cancelled — you can sign in again'
    );
    expect(authApi.cancelDeletionByToken).toHaveBeenCalledWith('abc123');
  });

  it('shows the server error message when the token is refused', async () => {
    authApi.cancelDeletionByToken.mockResolvedValue({ success: false, message: 'This link has already been used.' });
    renderPage();

    expect(await screen.findByTestId('cancel-deletion-error')).toHaveTextContent('This link has already been used.');
  });

  it('shows a generic error when the request throws', async () => {
    authApi.cancelDeletionByToken.mockRejectedValue(new Error('network down'));
    renderPage();

    expect(await screen.findByTestId('cancel-deletion-error')).toHaveTextContent(
      'This link is invalid or has expired.'
    );
  });

  it('shows an error immediately when the URL has no token, without calling the API', async () => {
    renderPage('');
    expect(await screen.findByTestId('cancel-deletion-error')).toBeInTheDocument();
    expect(authApi.cancelDeletionByToken).not.toHaveBeenCalled();
  });
});
