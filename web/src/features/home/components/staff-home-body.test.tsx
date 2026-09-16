import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { makeHomeDto, makeStaffHome } from '../test/fixtures';

const useHomeMock = vi.fn();
vi.mock('../hooks/use-home', () => ({
  useHome: () => useHomeMock(),
}));
vi.mock('./caseload-home', () => ({
  CaseloadHome: ({ staff }: { staff: { variant: string } }) => (
    <div data-testid="caseload-home">{staff.variant}</div>
  ),
}));
vi.mock('./admin-home', () => ({
  AdminHome: ({ isDistrict }: { isDistrict: boolean }) => (
    <div data-testid="admin-home">{isDistrict ? 'district' : 'school'}</div>
  ),
}));

import { StaffHomeBody } from './staff-home-body';

function renderBody() {
  return render(
    <MemoryRouter>
      <StaffHomeBody />
    </MemoryRouter>
  );
}

describe('StaffHomeBody', () => {
  beforeEach(() => {
    useHomeMock.mockReset();
  });

  it('shows a loading skeleton while the home fetch is in flight', () => {
    useHomeMock.mockReturnValue({ home: null, isLoading: true, error: null, retry: vi.fn() });
    renderBody();
    expect(screen.getByTestId('staff-home-loading')).toBeInTheDocument();
  });

  it('renders an error notice with retry on load failure — never an empty state', async () => {
    const user = userEvent.setup();
    const retry = vi.fn();
    useHomeMock.mockReturnValue({
      home: null,
      isLoading: false,
      error: "Couldn't load your home",
      retry,
    });
    renderBody();

    expect(screen.getByTestId('staff-home-error')).toBeInTheDocument();
    expect(screen.getByText("Couldn't load your home")).toBeInTheDocument();
    await user.click(screen.getByTestId('staff-home-retry'));
    expect(retry).toHaveBeenCalledTimes(1);
  });

  it('dispatches to CaseloadHome for a Teacher/CaseManager variant', () => {
    useHomeMock.mockReturnValue({
      home: makeHomeDto({ staff: makeStaffHome({ variant: 'CaseManager' }) }),
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });
    renderBody();
    expect(screen.getByTestId('caseload-home')).toHaveTextContent('CaseManager');
  });

  it('dispatches to AdminHome for SchoolAdmin/DistrictAdmin variants', () => {
    useHomeMock.mockReturnValue({
      home: makeHomeDto({ staff: makeStaffHome({ variant: 'DistrictAdmin' }) }),
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });
    renderBody();
    expect(screen.getByTestId('admin-home')).toHaveTextContent('district');
  });
});
