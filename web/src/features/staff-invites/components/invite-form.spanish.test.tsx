import '@/app/lazy-routes/staff-locales';
import { describe, it, expect, vi, afterEach } from 'vitest';
import { screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import { ORG_ROLE } from '@/features/educator/types';
import { InviteForm } from './invite-form';

const schools = [
  { id: 5, name: 'Lincoln Elementary', activeStudentCount: 0, activeStaffCount: 0 },
];

describe('InviteForm in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it("translates the DistrictAdmin's invitable roles", async () => {
    await renderInSpanish(
      <MemoryRouter>
        <InviteForm callerOrgRoleId={ORG_ROLE.DistrictAdmin} schools={schools} onSubmit={vi.fn()} />
      </MemoryRouter>,
      { ns: 'staff-invites' }
    );

    const roleOptions = within(screen.getByLabelText('Rol *'))
      .getAllByRole('option')
      .map((o) => o.textContent);

    expect(roleOptions).toEqual([
      'Administrador del distrito',
      'Administrador escolar',
      'Maestro',
      'Proveedor de servicios relacionados',
      'Maestro de educación general',
    ]);
    expect(screen.getByRole('button', { name: 'Enviar invitación' })).toBeInTheDocument();
  });
});
