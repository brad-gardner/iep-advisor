import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { type LoadError, toLoadError, loadErrorText } from '@/lib/api-error';
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
  const { t } = useTranslation('family-contact');
  const [attempts, setAttempts] = useState<FamilyContactAttemptDto[]>([]);
  const [offlineInput, setOfflineInput] = useState<OfflineFamilyInputDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<LoadError | null>(null);
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
          setError(toLoadError({ message: attemptsRes.message ?? inputRes.message }));
        }
      } catch (err) {
        if (!active) return;
        setError(toLoadError(err));
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded (see `use-home.ts`): re-running this fetch on
    // a plain language switch would be wasteful.
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
    setRetryToken((n) => n + 1);
  }, []);

  return {
    attempts,
    offlineInput,
    isLoading,
    error: loadErrorText(error, t('card.loadFailed')),
    retry,
    addAttempt,
    addOfflineInput,
  };
}
