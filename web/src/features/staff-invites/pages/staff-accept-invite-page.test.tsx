import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { StaffInvitePreview } from '../types';

const useAuthMock = vi.hoisted(() => vi.fn());
vi.mock('@/features/auth/hooks/use-auth', () => ({ useAuth: useAuthMock }));

const previewStaffInvite = vi.hoisted(() => vi.fn());
vi.mock('../api/staff-invites-api', () => ({
  previewStaffInvite,
  acceptStaffInvite: vi.fn(),
}));

import { StaffAcceptInvitePage } from './staff-accept-invite-page';

function makePreview(overrides: Partial<StaffInvitePreview> = {}): StaffInvitePreview {
  return {
    status: 'valid',
    email: 'pat@example.com',
    districtName: 'Springfield Unified School District',
    schoolName: null,
    roleName: 'DistrictAdmin',
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/staff/accept-invite?token=abc']}>
      <StaffAcceptInvitePage />
    </MemoryRouter>
  );
}

describe('StaffAcceptInvitePage', () => {
  afterEach(() => {
    vi.clearAllMocks();
    void resetTestLanguage();
  });

  it('renders the district, role, and school as one sentence — not a raw role name', async () => {
    useAuthMock.mockReturnValue({ user: null, applySession: vi.fn(), logout: vi.fn() });
    previewStaffInvite.mockResolvedValue({ success: true, data: makePreview({ schoolName: 'Lincoln Elementary' }) });

    renderPage();

    // The district name is its own `<span>`, so `.closest('p')` reaches the
    // whole rendered sentence (`getByText`'s default matcher only reads a
    // node's own direct text, not text split across nested elements).
    const district = await screen.findByText('Springfield Unified School District');
    expect(district.closest('p')).toHaveTextContent(
      'Springfield Unified School District · Lincoln Elementary invited you to join as District administrator.'
    );
  });

  it('renders the whole sentence in Spanish, with the role translated too (no mixed-language text)', async () => {
    useAuthMock.mockReturnValue({ user: null, applySession: vi.fn(), logout: vi.fn() });
    previewStaffInvite.mockResolvedValue({ success: true, data: makePreview({ roleName: 'Teacher' }) });

    await renderInSpanish(
      <MemoryRouter initialEntries={['/staff/accept-invite?token=abc']}>
        <StaffAcceptInvitePage />
      </MemoryRouter>
    );

    const district = await screen.findByText('Springfield Unified School District');
    expect(district.closest('p')).toHaveTextContent(
      'Springfield Unified School District le invitó a unirse como Maestro.'
    );
    // No leftover English role name anywhere on the page.
    expect(document.body.textContent).not.toMatch(/Teacher/);
  });
});
