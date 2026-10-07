import '@/app/lazy-routes/staff-locales';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { ORG_ROLE, type EducatorProfile } from '@/features/educator/types';
import type { ComplianceBoardDto } from '../types';

const useEducatorProfileMock = vi.fn();
vi.mock('@/features/educator/hooks/use-educator-profile', () => ({
  useEducatorProfile: () => useEducatorProfileMock(),
}));

const districtApi = vi.hoisted(() => ({
  getComplianceBoard: vi.fn(),
  getDistrictSchools: vi.fn(),
  getAdoption: vi.fn(),
  getEngagement: vi.fn(),
}));
vi.mock('@/features/district-admin/api/district-api', () => districtApi);

import { ComplianceBoardPage } from './compliance-board-page';

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

function makeBoard(overrides: Partial<ComplianceBoardDto> = {}): ComplianceBoardDto {
  return {
    generatedAt: '2026-09-16T00:00:00.000Z',
    from: '2026-09-16',
    to: '2026-11-15',
    summary: {
      overdueAnnual: 3,
      overdueReeval: 1,
      due30: 5,
      due60: 8,
      unknownDates: 2,
      noLead: 4,
      activeStudents: 100,
      dueInRange: 6,
    },
    bySchool: [
      {
        schoolId: 5,
        schoolName: 'Lincoln Elementary',
        activeStudents: 60,
        overdueAnnual: 2,
        overdueReeval: 1,
        due30: 3,
        due60: 4,
        unknownDates: 1,
        noLead: 2,
        dueInRange: 3,
      },
    ],
    drill: {
      overdueAnnual: 'attention=OverdueAnnual',
      overdueReeval: 'attention=OverdueReeval',
      due30: 'attention=Due30',
      due60: 'attention=Due60',
      unknownDates: 'attention=UnknownDates',
      noLead: 'attention=NoCaseManager',
      dueInRange: 'attention=DueInRange&from=2026-09-16&to=2026-11-15',
    },
    ...overrides,
  };
}

describe('ComplianceBoardPage in Spanish', () => {
  beforeEach(() => {
    Object.values(districtApi).forEach((fn) => fn.mockReset());
    districtApi.getComplianceBoard.mockResolvedValue({ success: true, data: makeBoard() });
    districtApi.getDistrictSchools.mockResolvedValue({
      success: true,
      data: [{ id: 5, name: 'Lincoln Elementary', activeStudentCount: 60, activeStaffCount: 10 }],
    });
    districtApi.getAdoption.mockResolvedValue({
      success: true,
      data: {
        days: 14,
        staffActiveLast14: 8,
        staffTotal: 10,
        bySchool: [],
        draftsStarted: 4,
        draftsFinalized: 2,
        activeRule: 'Logged in or edited a document in the window',
      },
    });
    districtApi.getEngagement.mockResolvedValue({
      success: true,
      data: { studentsWithFamilyLink: 40, activeStudents: 60, draftsShared: 0, responsesReceived: 0, bySchool: [] },
    });
    useEducatorProfileMock.mockReturnValue({ profile: makeProfile() });
  });

  afterEach(() => resetTestLanguage());

  it('renders the heading and the compliance tile labels in Spanish', async () => {
    await renderInSpanish(
      <MemoryRouter>
        <ComplianceBoardPage />
      </MemoryRouter>,
      { ns: 'district-admin' }
    );

    expect(screen.getByRole('heading', { name: 'Cumplimiento' })).toBeInTheDocument();

    await waitFor(() => expect(screen.getByTestId('compliance-summary-tiles')).toBeInTheDocument());
    expect(screen.getByTestId('compliance-summary-overdueAnnual')).toHaveTextContent('Revisiones anuales vencidas');
    expect(screen.getByTestId('compliance-summary-dueInRange')).toHaveTextContent('Vence en el rango seleccionado');
  });
});
