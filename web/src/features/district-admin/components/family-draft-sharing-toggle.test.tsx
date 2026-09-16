import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ToastProvider } from '@/components/ui/toast';
import { apiRejection } from '@/test/axios-rejection';
import type { DistrictOverview } from '../types';

const districtApi = vi.hoisted(() => ({
  getDistrict: vi.fn(),
  updateDistrict: vi.fn(),
}));
vi.mock('../api/district-api', () => districtApi);

import { FamilyDraftSharingToggle } from './family-draft-sharing-toggle';

function makeOverview(overrides: Partial<DistrictOverview> = {}): DistrictOverview {
  return {
    id: 1,
    name: 'Test District',
    stateCode: 'OH',
    activeSchoolCount: 3,
    activeStaffCount: 12,
    familyDraftSharingEnabled: true,
    ...overrides,
  };
}

function renderToggle() {
  render(
    <ToastProvider>
      <FamilyDraftSharingToggle />
    </ToastProvider>
  );
}

describe('FamilyDraftSharingToggle', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows the current setting once loaded', async () => {
    districtApi.getDistrict.mockResolvedValue({ success: true, data: makeOverview({ familyDraftSharingEnabled: true }) });
    renderToggle();

    const toggle = await screen.findByTestId('family-sharing-toggle');
    expect(toggle).toHaveAttribute('aria-checked', 'true');
  });

  it('toggles the district setting off and back on', async () => {
    const user = userEvent.setup();
    districtApi.getDistrict.mockResolvedValue({ success: true, data: makeOverview({ familyDraftSharingEnabled: true }) });
    districtApi.updateDistrict.mockResolvedValue({ success: true, data: makeOverview({ familyDraftSharingEnabled: false }) });
    renderToggle();

    const toggle = await screen.findByTestId('family-sharing-toggle');
    await user.click(toggle);

    await waitFor(() => expect(districtApi.updateDistrict).toHaveBeenCalledWith({ familyDraftSharingEnabled: false }));
    expect(toggle).toHaveAttribute('aria-checked', 'false');
  });

  it('surfaces a server refusal inline', async () => {
    const user = userEvent.setup();
    districtApi.getDistrict.mockResolvedValue({ success: true, data: makeOverview({ familyDraftSharingEnabled: true }) });
    districtApi.updateDistrict.mockRejectedValue(apiRejection('Could not update this setting.'));
    renderToggle();

    await user.click(await screen.findByTestId('family-sharing-toggle'));

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not update this setting.');
  });
});
