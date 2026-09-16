import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { ToastProvider } from '@/components/ui/toast';
import { apiRejection } from '@/test/axios-rejection';
import { makeStudent } from '../test/fixtures';

const api = vi.hoisted(() => ({
  getStudent: vi.fn(),
  updateStudent: vi.fn(),
  exitStudent: vi.fn(),
  reactivateStudent: vi.fn(),
  archiveStudent: vi.fn(),
  transferStudent: vi.fn(),
}));
vi.mock('../api/educator-api', () => api);

import { useStudentRecord } from './use-student-record';

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <ToastProvider>{children}</ToastProvider>
);

const request = {
  firstName: 'Ada', lastName: 'Lovelace', dateOfBirth: null, stateCode: null,
  externalStudentId: '000123', gradeLevel: null, disabilityCategory: null, homeLanguage: null,
  iepDate: null, annualReviewDueDate: null, etrDate: null, reevaluationDueDate: null,
};

describe('useStudentRecord', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getStudent.mockResolvedValue({ success: true, data: makeStudent() });
  });

  it('adopts the DTO a successful mutation returns', async () => {
    api.updateStudent.mockResolvedValue({ success: true, data: makeStudent({ firstName: 'Ada B.' }) });
    const { result } = renderHook(() => useStudentRecord(10), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let outcome: { success: boolean; error?: string } | undefined;
    await act(async () => {
      outcome = await result.current.update(request);
    });
    expect(outcome).toEqual({ success: true });
    expect(result.current.student?.firstName).toBe('Ada B.');
  });

  it('surfaces the envelope message from a 4xx rejection', async () => {
    api.updateStudent.mockRejectedValue(apiRejection('Student ID already in use in this district.'));
    const { result } = renderHook(() => useStudentRecord(10), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let outcome: { success: boolean; error?: string } | undefined;
    await act(async () => {
      outcome = await result.current.update(request);
    });
    expect(outcome).toEqual({ success: false, error: 'Student ID already in use in this district.' });
    expect(result.current.student?.firstName).toBe('Ada');
  });

  it('falls back to the action-specific message for a non-envelope failure', async () => {
    api.transferStudent.mockRejectedValue(new Error('network'));
    const { result } = renderHook(() => useStudentRecord(10), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let outcome: { success: boolean; error?: string } | undefined;
    await act(async () => {
      outcome = await result.current.transfer({ newSchoolId: 6 });
    });
    expect(outcome).toEqual({ success: false, error: 'Could not transfer the student' });
  });
});
