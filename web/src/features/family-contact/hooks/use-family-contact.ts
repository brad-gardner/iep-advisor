import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getContactAttempts, getOfflineInput } from '../api/family-contact-api';
import type { FamilyContactAttemptDto, OfflineFamilyInputDto } from '../types';

interface UseFamilyContactResult {
  attempts: FamilyContactAttemptDto[];
  offlineInput: OfflineFamilyInputDto[];
  isLoading: boolean;
  error: string | null;
  retry: () => void;
  addAttempt: (attempt: FamilyContactAttemptDto) => void;
  addOfflineInput: (input: OfflineFamilyInputDto) => void;
}

/** A student's offline family-contact history for the "Family contact" card
 *  (plan 7, decision 7): contact attempts and offline input, newest first. */
export function useFamilyContact(studentId: number): UseFamilyContactResult {
  const [attempts, setAttempts] = useState<FamilyContactAttemptDto[]>([]);
  const [offlineInput, setOfflineInput] = useState<OfflineFamilyInputDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    if (!studentId) return;
    let active = true;
    (async () => {
      try {
        const [attemptsRes, inputRes] = await Promise.all([
          getContactAttempts(studentId),
          getOfflineInput(studentId),
        ]);
        if (!active) return;
        if (attemptsRes.success && attemptsRes.data && inputRes.success && inputRes.data) {
          setAttempts(attemptsRes.data);
          setOfflineInput(inputRes.data);
          setError(null);
        } else {
          setError(attemptsRes.message ?? inputRes.message ?? 'Could not load family contact history.');
        }
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load family contact history.'));
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [studentId, retryToken]);

  const addAttempt = useCallback((attempt: FamilyContactAttemptDto) => {
    setAttempts((prev) => [attempt, ...prev]);
  }, []);

  const addOfflineInput = useCallback((input: OfflineFamilyInputDto) => {
    setOfflineInput((prev) => [input, ...prev]);
  }, []);

  const retry = useCallback(() => {
    setIsLoading(true);
    setError(null);
    setRetryToken((t) => t + 1);
  }, []);

  return {
    attempts,
    offlineInput,
    isLoading,
    error,
    retry,
    addAttempt,
    addOfflineInput,
  };
}
