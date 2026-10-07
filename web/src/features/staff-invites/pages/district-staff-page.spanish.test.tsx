import '@/app/lazy-routes/staff-locales';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import userEvent from '@testing-library/user-event';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { ORG_ROLE, type EducatorProfile } from '@/features/educator/types';

const useEducatorProfileMock = vi.fn();
vi.mock('@/features/educator/hooks/use-educator-profile', () => ({
  useEducatorProfile: () => useEducatorProfileMock(),
}));

const staffApi = vi.hoisted(() => ({
  getStaffList: vi.fn(),
  createStaffInvite: vi.fn(),
  deactivateStaff: vi.fn(),
  reactivateStaff: vi.fn(),
  resendStaffInvite: vi.fn(),
  revokeStaffInvite: vi.fn(),
}));
vi.mock('../api/staff-invites-api', () => staffApi);

const districtApi = vi.hoisted(() => ({ getDistrictSchools: vi.fn() }));
vi.mock('@/features/district-admin/api/district-api', () => districtApi);

function makeProfile(overrides: Partial<EducatorProfile> = {}): EducatorProfile {
  return {
    staffProfileId: 1,
    userId: 1,
    orgRoleId: ORG_ROLE.DistrictAdmin,
    orgRoleName: 'DistrictAdmin',
    districtId: 1,
    districtName: 'Test District',
    schoolId: null,
    schoolName: null,
    isActive: true,
    stateCode: 'OH',
    title: null,
    credentials: null,
    ...overrides,
  };
}

import { DistrictStaffPage } from './district-staff-page';

describe('DistrictStaffPage in Spanish', () => {
  beforeEach(() => {
    Object.values(staffApi).forEach((fn) => fn.mockReset());
    districtApi.getDistrictSchools.mockReset();
    staffApi.getStaffList.mockResolvedValue({ success: true, data: { members: [], pendingInvites: [] } });
    districtApi.getDistrictSchools.mockResolvedValue({
      success: true,
      data: [{ id: 5, name: 'Lincoln Elementary', activeStudentCount: 60, activeStaffCount: 10 }],
    });
    useEducatorProfileMock.mockReturnValue({ profile: makeProfile() });
  });

  afterEach(() => resetTestLanguage());

  it('renders the heading and section headings in Spanish, and opens the invite modal in Spanish', async () => {
    const user = userEvent.setup();
    await renderInSpanish(
      <ToastProvider>
        <MemoryRouter>
          <DistrictStaffPage />
        </MemoryRouter>
      </ToastProvider>,
      { ns: ['staff-invites', 'district-admin'] }
    );

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Personal', level: 2 })).toBeInTheDocument());
    expect(screen.getByRole('heading', { name: 'Invitaciones pendientes' })).toBeInTheDocument();

    await user.click(screen.getByTestId('district-staff-invite-open'));
    expect(screen.getByRole('heading', { name: 'Invitar a un miembro del personal' })).toBeInTheDocument();
    expect(screen.getByLabelText('Correo electrónico laboral *')).toBeInTheDocument();
  });
});
