import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useStudentTeam } from './use-student-team';

const educatorApi = vi.hoisted(() => ({
  getTeam: vi.fn(),
}));
vi.mock('@/features/educator/api/educator-api', () => educatorApi);

const member = {
  id: 1,
  userId: 7,
  staffProfileId: 1,
  firstName: 'Ana',
  lastName: 'Ito',
  email: 'ana@example.com',
  orgRoleName: 'RelatedServiceProvider',
  teamRole: 'OccupationalTherapist',
  isLead: false,
  accessRole: 'Collaborator',
  isActive: true,
  addedAt: '2026-01-01T00:00:00Z',
} as const;

describe('useStudentTeam', () => {
  it('fetches eagerly on mount and exposes the members on success', async () => {
    educatorApi.getTeam.mockResolvedValueOnce({ success: true, data: [member] });
    const { result } = renderHook(() => useStudentTeam(42));

    expect(result.current.isLoading).toBe(true);
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(educatorApi.getTeam).toHaveBeenCalledWith(42);
    expect(result.current.isError).toBe(false);
    expect(result.current.members).toEqual([member]);
  });

  it('exposes an error state when the request fails', async () => {
    educatorApi.getTeam.mockRejectedValueOnce(new Error('network error'));
    const { result } = renderHook(() => useStudentTeam(42));

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isError).toBe(true);
    expect(result.current.members).toEqual([]);
  });

  it('exposes an error state when the envelope reports failure', async () => {
    educatorApi.getTeam.mockResolvedValueOnce({ success: false, message: 'nope' });
    const { result } = renderHook(() => useStudentTeam(42));

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isError).toBe(true);
  });

  it('re-fetches when the student id changes', async () => {
    educatorApi.getTeam.mockResolvedValue({ success: true, data: [member] });
    const { rerender } = renderHook(({ studentId }) => useStudentTeam(studentId), {
      initialProps: { studentId: 1 },
    });
    await waitFor(() => expect(educatorApi.getTeam).toHaveBeenCalledWith(1));

    rerender({ studentId: 2 });
    await waitFor(() => expect(educatorApi.getTeam).toHaveBeenCalledWith(2));
  });
});
