import { useEffect, useRef, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getOutboundEmailStatus, listOutboundEmails } from '../api/email-admin-api';
import { IN_FLIGHT_EMAIL_STATUSES } from '../types';
import type { OutboundEmailDto, OutboundEmailStatusDto, OutboundEmailStatusFilter } from '../types';

const POLL_INTERVAL_MS = 15_000;

interface UseOutboundEmailsResult {
  emails: OutboundEmailDto[];
  status: OutboundEmailStatusDto | null;
  isLoading: boolean;
  error: string | null;
  reload: () => void;
}

/** Platform-admin `/admin/email`: loads the filtered list plus the delivery
 *  status banner, then polls every 15s while any row is Queued/Sending under
 *  the *current* filter — same "latest response wins" shape as
 *  `useDistrictExports` (plan 7, decision 8), extended with a reactive status
 *  filter: changing it restarts the effect (a fresh load, its own generation
 *  counter, and its own poll). */
export function useOutboundEmails(status: OutboundEmailStatusFilter): UseOutboundEmailsResult {
  const [emails, setEmails] = useState<OutboundEmailDto[]>([]);
  const [emailStatus, setEmailStatus] = useState<OutboundEmailStatusDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const emailsRef = useRef<OutboundEmailDto[]>([]);
  const reloadRef = useRef<() => void>(() => {});

  useEffect(() => {
    let active = true;
    // Latest request wins: a slow poll (or a filter switch) that resolves
    // after a newer one must not roll the table back to a stale snapshot.
    // Every setState below runs inside `load`'s async body, never
    // synchronously in the effect itself.
    let generation = 0;

    async function load() {
      const mine = ++generation;
      try {
        const [listRes, statusRes] = await Promise.all([
          listOutboundEmails(status),
          getOutboundEmailStatus(),
        ]);
        if (!active || mine !== generation) return;
        if (listRes.success && listRes.data) {
          emailsRef.current = listRes.data;
          setEmails(listRes.data);
          setError(null);
        } else {
          setError(listRes.message ?? 'Could not load outbound emails.');
        }
        if (statusRes.success && statusRes.data) setEmailStatus(statusRes.data);
      } catch (err) {
        if (active && mine === generation) setError(apiErrorMessage(err, 'Could not load outbound emails.'));
      } finally {
        if (active && mine === generation) setIsLoading(false);
      }
    }

    reloadRef.current = () => void load();

    void load();
    const interval = setInterval(() => {
      if (emailsRef.current.some((e) => IN_FLIGHT_EMAIL_STATUSES.has(e.status))) {
        void load();
      }
    }, POLL_INTERVAL_MS);

    return () => {
      active = false;
      clearInterval(interval);
    };
  }, [status]);

  return {
    emails,
    status: emailStatus,
    isLoading,
    error,
    reload: () => reloadRef.current(),
  };
}
