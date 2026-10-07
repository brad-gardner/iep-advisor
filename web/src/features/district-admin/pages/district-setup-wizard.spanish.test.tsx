import '@/app/lazy-routes/staff-locales';
import { describe, it, expect, vi, afterEach } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { ORG_ROLE, type EducatorProfile } from '@/features/educator/types';

const useEducatorProfileMock = vi.fn();
vi.mock('@/features/educator/hooks/use-educator-profile', () => ({
  useEducatorProfile: () => useEducatorProfileMock(),
}));

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

import { DistrictSetupWizard } from './district-setup-wizard';

describe('DistrictSetupWizard in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the welcome step in Spanish', async () => {
    useEducatorProfileMock.mockReturnValue({ profile: makeProfile(), isLoading: false });

    await renderInSpanish(
      <MemoryRouter>
        <DistrictSetupWizard />
      </MemoryRouter>,
      { ns: 'district-admin' }
    );

    expect(screen.getByRole('heading', { name: 'Bienvenido, Test District' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Comenzar configuración' })).toBeInTheDocument();
    expect(screen.getByText('Paso 1 de 4')).toBeInTheDocument();
  });
});
