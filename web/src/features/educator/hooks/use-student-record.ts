import { useCallback, useEffect, useState } from 'react';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import {
  archiveStudent,
  exitStudent,
  getStudent,
  reactivateStudent,
  transferStudent,
  updateStudent,
} from '../api/educator-api';
import type {
  ExitStudentRequest,
  SchoolStudent,
  TransferStudentRequest,
  UpdateSchoolStudentRequest,
} from '../types';

export type ActionResult = { success: boolean; error?: string };

interface UseStudentRecordResult {
  student: SchoolStudent | null;
  isLoading: boolean;
  reload: () => Promise<void>;
  update: (data: UpdateSchoolStudentRequest) => Promise<ActionResult>;
  exit: (data: ExitStudentRequest) => Promise<ActionResult>;
  reactivate: () => Promise<ActionResult>;
  archive: () => Promise<ActionResult>;
  transfer: (data: TransferStudentRequest) => Promise<ActionResult>;
}

// Loads one student and exposes its lifecycle mutations. Each mutation adopts
// the DTO the server returns (no refetch) and toasts on success; a failure
// comes back as `{ success: false, error }` for the calling form/dialog to
// render inline.
export function useStudentRecord(studentId: number): UseStudentRecordResult {
  const { show: showToast } = useToast();
  const [student, setStudent] = useState<SchoolStudent | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await getStudent(studentId);
        if (active && response.success && response.data) setStudent(response.data);
      } catch {
        // A server/network error leaves `student` null → "not found" renders.
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [studentId]);

  const reload = useCallback(async () => {
    try {
      const response = await getStudent(studentId);
      if (response.success && response.data) setStudent(response.data);
    } catch {
      // Keep the current record on a transient failure.
    }
  }, [studentId]);

  const run = useCallback(
    async (
      call: () => Promise<{ success: boolean; message?: string; data?: SchoolStudent }>,
      successMessage: string,
      fallback: string
    ): Promise<ActionResult> => {
      try {
        const response = await call();
        if (response.success && response.data) {
          setStudent(response.data);
          showToast({ message: successMessage, variant: 'success' });
          return { success: true };
        }
        return { success: false, error: response.message || fallback };
      } catch (err) {
        // 400/403 refusals arrive as rejections carrying the envelope message.
        return { success: false, error: apiErrorMessage(err, fallback) };
      }
    },
    [showToast]
  );

  return {
    student,
    isLoading,
    reload,
    update: (data) =>
      run(() => updateStudent(studentId, data), 'Student updated', 'Could not save the student'),
    exit: (data) =>
      run(() => exitStudent(studentId, data), 'Student exited', 'Could not exit the student'),
    reactivate: () =>
      run(() => reactivateStudent(studentId), 'Student reactivated', 'Could not reactivate the student'),
    archive: () =>
      run(() => archiveStudent(studentId), 'Student archived', 'Could not archive the student'),
    transfer: (data) =>
      run(() => transferStudent(studentId, data), 'Student transferred', 'Could not transfer the student'),
  };
}
