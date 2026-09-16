import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { OutboundEmailDto } from '../types';

const api = vi.hoisted(() => ({
  listOutboundEmails: vi.fn(),
  getOutboundEmailStatus: vi.fn(),
  resendOutboundEmail: vi.fn(),
  cancelOutboundEmail: vi.fn(),
}));
vi.mock('../api/email-admin-api', () => api);

const showToast = vi.hoisted(() => vi.fn());
vi.mock('@/components/ui/toast', () => ({ useToast: () => ({ show: showToast }) }));

import { AdminEmailPage } from './admin-email-page';

function email(overrides: Partial<OutboundEmailDto> = {}): OutboundEmailDto {
  return {
    id: 1,
    toEmail: 'parent@example.com',
    subject: 'Meeting reminder',
    kind: 'MeetingReminder',
    status: 'Failed',
    attempts: 3,
    lastError: 'ACS send failed: 503',
    nextAttemptAt: '2026-09-16T00:00:00.000Z',
    sentAt: null,
    correlationId: null,
    createdAt: '2026-09-15T00:00:00.000Z',
    ...overrides,
  };
}

const configuredStatus = { success: true, data: { configured: true, queued: 0, failed: 1, sending: 0, lastSentAt: null } };
const unconfiguredStatus = { success: true, data: { configured: false, queued: 0, failed: 0, sending: 0, lastSentAt: null } };

describe('AdminEmailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getOutboundEmailStatus.mockResolvedValue(configuredStatus);
  });

  it('shows a warning banner when email delivery is not configured', async () => {
    api.listOutboundEmails.mockResolvedValue({ success: true, data: [] });
    api.getOutboundEmailStatus.mockResolvedValue(unconfiguredStatus);

    render(<AdminEmailPage />);

    expect(await screen.findByTestId('email-unconfigured-banner')).toHaveTextContent(
      'Email delivery is not configured'
    );
    expect(screen.queryByTestId('email-status-summary')).not.toBeInTheDocument();
  });

  it('shows the queue summary instead of the banner when configured', async () => {
    api.listOutboundEmails.mockResolvedValue({ success: true, data: [] });

    render(<AdminEmailPage />);

    expect(await screen.findByTestId('email-status-summary')).toHaveTextContent('0 queued · 0 sending · 1 failed');
    expect(screen.queryByTestId('email-unconfigured-banner')).not.toBeInTheDocument();
  });

  it('defaults the filter to Failed', async () => {
    api.listOutboundEmails.mockResolvedValue({ success: true, data: [] });
    render(<AdminEmailPage />);
    await waitFor(() => expect(api.listOutboundEmails).toHaveBeenCalledWith('Failed'));
  });

  it('resends a Failed email after confirmation, toasts, and reloads the list', async () => {
    const user = userEvent.setup();
    api.listOutboundEmails.mockResolvedValue({ success: true, data: [email()] });
    api.resendOutboundEmail.mockResolvedValue({ success: true, data: null });

    render(<AdminEmailPage />);
    await screen.findByText('parent@example.com');

    await user.click(screen.getByRole('button', { name: 'Actions for parent@example.com' }));
    await user.click(screen.getByTestId('email-resend-1'));
    expect(screen.getByRole('alertdialog', { name: 'Resend email' })).toBeInTheDocument();

    await user.click(screen.getByTestId('email-action-confirm-confirm'));

    await waitFor(() => expect(api.resendOutboundEmail).toHaveBeenCalledWith(1));
    expect(showToast).toHaveBeenCalledWith({ message: 'Email to parent@example.com re-queued', variant: 'success' });
    await waitFor(() => expect(screen.queryByRole('alertdialog', { name: 'Resend email' })).not.toBeInTheDocument());
    // Reloaded after the mutation (initial load + reload).
    await waitFor(() => expect(api.listOutboundEmails).toHaveBeenCalledTimes(2));
  });

  it('cancels a Queued email after confirmation', async () => {
    const user = userEvent.setup();
    api.listOutboundEmails.mockResolvedValue({ success: true, data: [email({ status: 'Queued', lastError: null })] });
    api.cancelOutboundEmail.mockResolvedValue({ success: true, data: null });

    render(<AdminEmailPage />);
    await screen.findByText('parent@example.com');

    await user.click(screen.getByRole('button', { name: 'Actions for parent@example.com' }));
    expect(screen.queryByTestId('email-resend-1')).not.toBeInTheDocument();
    await user.click(screen.getByTestId('email-cancel-1'));
    await user.click(screen.getByTestId('email-action-confirm-confirm'));

    await waitFor(() => expect(api.cancelOutboundEmail).toHaveBeenCalledWith(1));
    expect(showToast).toHaveBeenCalledWith({ message: 'Email to parent@example.com cancelled', variant: 'success' });
  });

  it('keeps the confirm dialog open with the error when the action fails', async () => {
    const user = userEvent.setup();
    api.listOutboundEmails.mockResolvedValue({ success: true, data: [email()] });
    api.resendOutboundEmail.mockResolvedValue({ success: false, message: 'Only a Failed or Cancelled email can be resent.' });

    render(<AdminEmailPage />);
    await screen.findByText('parent@example.com');

    await user.click(screen.getByRole('button', { name: 'Actions for parent@example.com' }));
    await user.click(screen.getByTestId('email-resend-1'));
    await user.click(screen.getByTestId('email-action-confirm-confirm'));

    expect(await screen.findByRole('alertdialog', { name: 'Resend email' })).toHaveTextContent(
      'Only a Failed or Cancelled email can be resent.'
    );
    expect(showToast).not.toHaveBeenCalled();
  });

  it('stops polling once no row is Queued/Sending', async () => {
    vi.useFakeTimers();
    try {
      api.listOutboundEmails.mockResolvedValue({ success: true, data: [email({ status: 'Sent', sentAt: '2026-09-16T00:00:00.000Z' })] });

      render(<AdminEmailPage />);
      // `vi.waitFor` (not RTL's `waitFor`, which polls via a real timer and
      // would deadlock under fake timers) — matches `vi.advanceTimersByTimeAsync` below.
      await vi.waitFor(() => expect(api.listOutboundEmails).toHaveBeenCalledTimes(1));

      await vi.advanceTimersByTimeAsync(15_000);
      expect(api.listOutboundEmails).toHaveBeenCalledTimes(1);
    } finally {
      vi.useRealTimers();
    }
  });
});
