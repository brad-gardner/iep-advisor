import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { ORG_ROLE, type EducatorProfile } from '@/features/educator/types';

const useEducatorProfileMock = vi.fn();

vi.mock('@/features/educator/hooks/use-educator-profile', () => ({
  useEducatorProfile: () => useEducatorProfileMock(),
}));
vi.mock('../components/staff-home-body', () => ({
  StaffHomeBody: () => <div data-testid="staff-home-body" />,
}));

import { StaffHomePage } from './staff-home-page';

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

function renderPage() {
  return renderInSpanish(
    <MemoryRouter>
      <StaffHomePage />
    </MemoryRouter>
  );
}

describe('StaffHomePage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('shows the loading status in Spanish', async () => {
    useEducatorProfileMock.mockReturnValue({ profile: null, isLoading: true });
    await renderPage();

    expect(screen.getByRole('status', { name: 'Cargando su inicio' })).toBeInTheDocument();
  });

  it('shows the "View students" action and org role in Spanish', async () => {
    useEducatorProfileMock.mockReturnValue({ profile: makeProfile(), isLoading: false });
    await renderPage();

    expect(screen.getByTestId('educator-students-link')).toHaveTextContent('Ver estudiantes');
    expect(screen.getByText(/Administrador del distrito/)).toBeInTheDocument();
  });
});
