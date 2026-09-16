import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const api = vi.hoisted(() => ({ requestMagicLink: vi.fn() }));
vi.mock('../api/auth-api', () => api);

import { MagicLinkRequestForm } from './magic-link-request-form';

describe('MagicLinkRequestForm', () => {
  beforeEach(() => vi.clearAllMocks());

  it('submits the trimmed email and shows the fixed confirmation on success', async () => {
    const user = userEvent.setup();
    api.requestMagicLink.mockResolvedValue({ success: true, data: null });

    render(<MagicLinkRequestForm />);
    await user.type(screen.getByTestId('magic-link-email'), '  staff@example.com  ');
    await user.click(screen.getByTestId('magic-link-submit'));

    expect(api.requestMagicLink).toHaveBeenCalledWith('staff@example.com');
    expect(await screen.findByTestId('magic-link-message')).toHaveTextContent(
      'If that address is eligible, a link is on its way.'
    );
  });

  it('shows the identical confirmation even when the request throws (no enumeration)', async () => {
    const user = userEvent.setup();
    api.requestMagicLink.mockRejectedValue(new Error('network down'));

    render(<MagicLinkRequestForm />);
    await user.type(screen.getByTestId('magic-link-email'), 'someone@example.com');
    await user.click(screen.getByTestId('magic-link-submit'));

    expect(await screen.findByTestId('magic-link-message')).toHaveTextContent(
      'If that address is eligible, a link is on its way.'
    );
  });
});
