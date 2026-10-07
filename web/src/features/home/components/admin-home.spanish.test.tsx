import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import {
  makeCaseManagerRow,
  makeComplianceSummary,
  makeHomeMeeting,
  makeRosterAttention,
  makeStaffHome,
  makeUnsignedFinalized,
} from '../test/fixtures';

vi.mock('@/features/district-admin/components/district-dashboard-tiles', () => ({
  DistrictDashboardTiles: () => <div data-testid="district-dashboard-tiles" />,
}));
vi.mock('@/features/district-admin/components/district-overview-card', () => ({
  DistrictOverviewCard: () => <div data-testid="district-overview-card" />,
}));
vi.mock('@/features/district-admin/components/setup-checklist-card', () => ({
  SetupChecklistCard: () => <div data-testid="setup-checklist-card" />,
}));
vi.mock('./adoption-engagement-teaser', () => ({
  AdoptionEngagementTeaser: () => <div data-testid="adoption-engagement-teaser" />,
}));

import { AdminHome } from './admin-home';

function renderAdmin(overrides: Parameters<typeof makeStaffHome>[0], isDistrict: boolean) {
  return renderInSpanish(
    <MemoryRouter>
      <AdminHome
        staff={makeStaffHome(overrides)}
        isDistrict={isDistrict}
        generatedAt="2026-09-16T00:00:00.000Z"
      />
    </MemoryRouter>
  );
}

describe('AdminHome in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('SchoolAdmin: translates the building-scoped week heading and the brief note', async () => {
    await renderAdmin({ variant: 'SchoolAdmin', meetingsThisWeek: [makeHomeMeeting()] }, false);

    expect(screen.getByText('Reuniones esta semana en mi escuela')).toBeInTheDocument();
    expect(screen.getByText('Resumen · próximamente')).toBeInTheDocument();
  });

  it('translates the overdue/at-risk table headers and the roster attention tiles', async () => {
    await renderAdmin(
      {
        variant: 'SchoolAdmin',
        overdueByCaseManager: [makeCaseManagerRow({ studentId: 1, studentName: 'Ada Lovelace' })],
        rosterAttention: makeRosterAttention(),
        unsignedFinalized: [makeUnsignedFinalized()],
      },
      false
    );

    const table = screen.getByTestId('home-overdue-by-case-manager-table');
    expect(table).toHaveTextContent('Estudiante');
    expect(table).toHaveTextContent('Administrador de caso');
    expect(screen.getByRole('heading', { name: 'Atención de la lista de estudiantes' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Documentos finalizados sin firmar' })).toBeInTheDocument();
  });

  it('DistrictAdmin: translates the compliance summary heading', async () => {
    await renderAdmin(
      { variant: 'DistrictAdmin', complianceSummary: makeComplianceSummary() },
      true
    );

    expect(screen.getByRole('heading', { name: 'Resumen de cumplimiento' })).toBeInTheDocument();
    expect(screen.getByText('Reuniones esta semana')).toBeInTheDocument();
  });
});
