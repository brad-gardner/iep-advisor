import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ToastProvider } from '@/components/ui/toast';
import { apiRejection } from '@/test/axios-rejection';
import type { ChildLink } from '../types';

const api = vi.hoisted(() => ({
  getStudentLinks: vi.fn(),
  inviteParent: vi.fn(),
  revokeStudentLink: vi.fn(),
}));
vi.mock('../api/educator-api', () => api);

import { FamilyLinksSection } from './family-links-section';

const link: ChildLink = {
  id: 55,
  schoolStudentId: 10,
  childProfileId: null,
  inviteEmail: 'parent@example.org',
  isActive: true,
  isAccepted: false,
  acceptedAt: null,
  linkedAt: null,
  inviteExpiresAt: null,
  createdAt: '2026-09-01T00:00:00Z',
};

describe('FamilyLinksSection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getStudentLinks.mockResolvedValue({ success: true, data: [link] });
  });

  it('keeps the revoke dialog open with the server message when the revoke is refused', async () => {
    const user = userEvent.setup();
    api.revokeStudentLink.mockRejectedValue(apiRejection('This link was already revoked.'));
    render(
      <ToastProvider>
        <FamilyLinksSection studentId={10} />
      </ToastProvider>
    );
    await user.click(await screen.findByTestId('student-link-revoke-55'));
    await user.click(screen.getByTestId('student-link-revoke-dialog-confirm'));

    expect(await screen.findByRole('alert')).toHaveTextContent('This link was already revoked.');
    expect(screen.getByTestId('student-link-revoke-dialog-cancel')).toBeInTheDocument();
    expect(api.getStudentLinks).toHaveBeenCalledTimes(1);
  });

  it('surfaces the server refusal when an invite is rejected', async () => {
    const user = userEvent.setup();
    api.inviteParent.mockRejectedValue(apiRejection('That parent already has a pending invite.'));
    render(
      <ToastProvider>
        <FamilyLinksSection studentId={10} />
      </ToastProvider>
    );
    await user.click(await screen.findByRole('button', { name: 'Invite parent' }));
    await user.type(screen.getByTestId('invite-parent-email'), 'parent2@example.org');
    await user.click(screen.getByTestId('invite-parent-submit'));
    expect(await screen.findByText('That parent already has a pending invite.')).toBeInTheDocument();
  });

  it('revokes a link, shows the forward-only note and reloads', async () => {
    const user = userEvent.setup();
    api.revokeStudentLink.mockResolvedValue({ success: true, data: null, message: 'Link revoked.' });
    api.getStudentLinks
      .mockResolvedValueOnce({ success: true, data: [link] })
      .mockResolvedValueOnce({ success: true, data: [{ ...link, isActive: false }] });
    render(
      <ToastProvider>
        <FamilyLinksSection studentId={10} />
      </ToastProvider>
    );
    await user.click(await screen.findByTestId('student-link-revoke-55'));
    await user.click(screen.getByTestId('student-link-revoke-dialog-confirm'));

    expect(await screen.findByText('Link revoked.')).toBeInTheDocument();
    await waitFor(() => expect(api.getStudentLinks).toHaveBeenCalledTimes(2));
    expect(screen.queryByTestId('student-link-revoke-55')).not.toBeInTheDocument();
  });
});
