import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { apiRejection } from '@/test/axios-rejection';

const api = vi.hoisted(() => ({
  getAdoption: vi.fn(),
  getEngagement: vi.fn(),
}));
vi.mock('../api/district-api', () => api);

import { useAdoptionEngagement } from './use-adoption-engagement';

const adoptionDto = {
  days: 14,
  staffActiveLast14: 8,
  staffTotal: 10,
  bySchool: [],
  draftsStarted: 4,
  draftsFinalized: 2,
  activeRule: 'Logged in or edited a document in the window',
};

const engagementDto = {
  studentsWithFamilyLink: 40,
  activeStudents: 60,
  draftsShared: 0,
  responsesReceived: 0,
  bySchool: [],
};

describe('useAdoptionEngagement', () => {
  beforeEach(() => {
    api.getAdoption.mockReset();
    api.getEngagement.mockReset();
  });

  it('returns both DTOs when both succeed', async () => {
    api.getAdoption.mockResolvedValue({ success: true, data: adoptionDto });
    api.getEngagement.mockResolvedValue({ success: true, data: engagementDto });

    const { result } = renderHook(() => useAdoptionEngagement(null));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.adoption).toEqual(adoptionDto);
    expect(result.current.engagement).toEqual(engagementDto);
    expect(result.current.error).toBeNull();
    expect(result.current.adoptionError).toBeNull();
    expect(result.current.engagementError).toBeNull();
  });

  it('keeps the adoption data when only engagement fails, instead of discarding it', async () => {
    api.getAdoption.mockResolvedValue({ success: true, data: adoptionDto });
    api.getEngagement.mockRejectedValue(apiRejection('Engagement is down'));

    const { result } = renderHook(() => useAdoptionEngagement(null));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.adoption).toEqual(adoptionDto);
    expect(result.current.engagement).toBeNull();
    expect(result.current.engagementError).toBe('Engagement is down');
    // Not a joint failure — the caller can still render the adoption tiles.
    expect(result.current.error).toBeNull();
  });

  it('keeps the engagement data when only adoption fails', async () => {
    api.getAdoption.mockResolvedValue({ success: false, message: 'Adoption is down' });
    api.getEngagement.mockResolvedValue({ success: true, data: engagementDto });

    const { result } = renderHook(() => useAdoptionEngagement(null));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.engagement).toEqual(engagementDto);
    expect(result.current.adoption).toBeNull();
    expect(result.current.adoptionError).toBe('Adoption is down');
    expect(result.current.error).toBeNull();
  });

  it('reports a joint error only when both endpoints fail', async () => {
    api.getAdoption.mockRejectedValue(apiRejection('Adoption is down'));
    api.getEngagement.mockRejectedValue(apiRejection('Engagement is down'));

    const { result } = renderHook(() => useAdoptionEngagement(null));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.adoption).toBeNull();
    expect(result.current.engagement).toBeNull();
    expect(result.current.error).toBe('Adoption is down');
  });
});
