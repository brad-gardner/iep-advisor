import '@/app/lazy-routes/staff-locales';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

const districtApi = vi.hoisted(() => ({
  getDistrictSchools: vi.fn(),
  createSchool: vi.fn(),
  updateSchool: vi.fn(),
  deactivateSchool: vi.fn(),
  getDistrict: vi.fn(),
  updateDistrict: vi.fn(),
}));
vi.mock('../api/district-api', () => districtApi);

vi.mock('@/features/educator/hooks/use-educator-profile', () => ({
  reloadEducatorProfile: vi.fn(),
}));

import { DistrictSchoolsPage } from './district-schools-page';

describe('DistrictSchoolsPage in Spanish', () => {
  beforeEach(() => {
    Object.values(districtApi).forEach((fn) => fn.mockReset());
    districtApi.getDistrictSchools.mockResolvedValue({
      success: true,
      data: [{ id: 5, name: 'Lincoln Elementary', stateCode: 'OH', activeStudentCount: 60, activeStaffCount: 10 }],
    });
    districtApi.getDistrict.mockResolvedValue({
      success: true,
      data: { id: 1, name: 'Test District', activeSchoolCount: 1, activeStaffCount: 10, familyDraftSharingEnabled: true },
    });
  });

  afterEach(() => resetTestLanguage());

  it('renders the heading, table columns and add-school button in Spanish', async () => {
    await renderInSpanish(
      <ToastProvider>
        <DistrictSchoolsPage />
      </ToastProvider>,
      { ns: 'district-admin' }
    );

    expect(screen.getByRole('heading', { name: 'Escuelas' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('district-schools-table')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /Agregar escuela/ })).toBeInTheDocument();
    expect(screen.getByTestId('district-schools-table')).toHaveTextContent('Estudiantes');
  });
});
