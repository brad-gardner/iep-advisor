import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ORG_ROLE } from '@/features/educator/types';
// `staff-invites` is a staff-only namespace (plan phase 6) — this component
// renders behind the lazy district-admin route chunk in the real app, which
// registers its English as a side effect of importing `staff-locales`; this
// test renders the component directly, so it imports the same module itself.
import '@/app/lazy-routes/staff-locales';
import { InviteForm } from './invite-form';

const schools = [
  { id: 5, name: 'Lincoln Elementary', activeStudentCount: 0, activeStaffCount: 0 },
];

function roleOptions() {
  return within(screen.getByLabelText('Role *'))
    .getAllByRole('option')
    .map((o) => o.textContent);
}

describe('InviteForm roles', () => {
  it('offers the provider and general-educator roles to a DistrictAdmin', () => {
    render(
      <MemoryRouter>
        <InviteForm callerOrgRoleId={ORG_ROLE.DistrictAdmin} schools={schools} onSubmit={vi.fn()} />
      </MemoryRouter>
    );
    expect(roleOptions()).toEqual([
      'District Admin',
      'School Admin',
      'Teacher',
      'Related service provider',
      'General educator',
    ]);
  });

  it('offers only school-bound roles to a SchoolAdmin', () => {
    render(
      <MemoryRouter>
        <InviteForm
          callerOrgRoleId={ORG_ROLE.SchoolAdmin}
          callerSchoolId={5}
          schools={schools}
          onSubmit={vi.fn()}
        />
      </MemoryRouter>
    );
    expect(roleOptions()).toEqual([
      'School Admin',
      'Teacher',
      'Related service provider',
      'General educator',
    ]);
  });
});
