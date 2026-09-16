import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';

const meetingsApi = vi.hoisted(() => ({ getDefaultParticipants: vi.fn() }));
vi.mock('../api/meetings-api', () => meetingsApi);

const educatorApi = vi.hoisted(() => ({ getEligibleTeamStaff: vi.fn() }));
vi.mock('@/features/educator/api/educator-api', () => educatorApi);

import { useMeetingParticipantPool } from './use-meeting-participant-pool';

const defaultParticipant = {
  userId: 7,
  displayName: 'Casey Manager',
  email: 'casey@district.org',
  teamRole: 'CaseManager' as const,
  isFamily: false,
  isStudent: false,
};

describe('useMeetingParticipantPool', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('seeds rows from both directories once they both succeed', async () => {
    meetingsApi.getDefaultParticipants.mockResolvedValue({ success: true, data: [defaultParticipant] });
    educatorApi.getEligibleTeamStaff.mockResolvedValue({ success: true, data: [] });

    const { result } = renderHook(() => useMeetingParticipantPool(10));
    await waitFor(() => expect(result.current.rows).not.toBeNull());

    expect(result.current.rows).toHaveLength(1);
    expect(result.current.failed).toBe(false);
  });

  it('keeps the successful directory\'s rows (not null) when the other one rejects', async () => {
    meetingsApi.getDefaultParticipants.mockResolvedValue({ success: true, data: [defaultParticipant] });
    educatorApi.getEligibleTeamStaff.mockRejectedValue(new Error('network error'));

    const { result } = renderHook(() => useMeetingParticipantPool(10));
    await waitFor(() => expect(result.current.failed).toBe(true));

    // The failed call didn't discard the other directory's real data.
    expect(result.current.rows).not.toBeNull();
    expect(result.current.rows).toHaveLength(1);
  });

  it('seeds an empty array (never null) so manual add is still possible when both directories fail', async () => {
    meetingsApi.getDefaultParticipants.mockRejectedValue(new Error('network error'));
    educatorApi.getEligibleTeamStaff.mockRejectedValue(new Error('network error'));

    const { result } = renderHook(() => useMeetingParticipantPool(10));
    await waitFor(() => expect(result.current.failed).toBe(true));

    expect(result.current.rows).toEqual([]);
  });
});
