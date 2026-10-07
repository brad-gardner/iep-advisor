import '@/app/lazy-routes/staff-locales';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { ORG_ROLE, type EducatorProfile } from '@/features/educator/types';

const useEducatorProfileMock = vi.fn();
vi.mock('@/features/educator/hooks/use-educator-profile', () => ({
  useEducatorProfile: () => useEducatorProfileMock(),
}));

const districtApi = vi.hoisted(() => ({
  getAuditLog: vi.fn(),
}));
vi.mock('../api/district-api', () => districtApi);

const staffApi = vi.hoisted(() => ({ getStaffList: vi.fn() }));
vi.mock('@/features/staff-invites/api/staff-invites-api', () => staffApi);

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

import { DistrictAuditLogPage } from './district-audit-log-page';

describe('DistrictAuditLogPage in Spanish', () => {
  beforeEach(() => {
    districtApi.getAuditLog.mockReset();
    staffApi.getStaffList.mockReset();
    staffApi.getStaffList.mockResolvedValue({ success: true, data: { members: [], pendingInvites: [] } });
    useEducatorProfileMock.mockReturnValue({ profile: makeProfile(), isLoading: false });
  });

  afterEach(() => resetTestLanguage());

  it('renders the heading and an empty-state in Spanish when there is no activity yet', async () => {
    districtApi.getAuditLog.mockResolvedValue({ success: true, data: { entries: [], nextCursor: null } });

    await renderInSpanish(<DistrictAuditLogPage />, { ns: 'district-admin' });

    expect(screen.getByRole('heading', { name: 'Registro de actividad' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('Aún no hay actividad')).toBeInTheDocument());
  });

  it('renders an activity row in Spanish', async () => {
    districtApi.getAuditLog.mockResolvedValue({
      success: true,
      data: {
        entries: [
          {
            id: 1,
            action: 'View',
            actorUserId: 2,
            actorName: 'Jane Teacher',
            resourceType: 'IepDocument',
            resourceId: 10,
            resourceDisplayName: 'IEP for Ada Lovelace',
            createdAt: '2026-09-16T00:00:00.000Z',
          },
        ],
        nextCursor: null,
      },
    });

    await renderInSpanish(<DistrictAuditLogPage />, { ns: 'district-admin' });

    const row = await screen.findByTestId('audit-row-1');
    expect(row).toHaveTextContent('vio');
  });
});
