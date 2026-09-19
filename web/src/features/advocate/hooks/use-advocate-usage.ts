import { useCallback, useEffect, useState } from 'react';
import { getAdvocateUsage } from '../api/advocate-api';
import type { AdvocateUsageDto } from '../types/advocate';

/**
 * This year's advocate allowance for the signed-in parent. Non-critical: a
 * failed fetch leaves `usage` null and the page simply shows no banner (the
 * server still enforces the cap on send). `reload` is called after each
 * answered message so the counter keeps up.
 */
export function useAdvocateUsage() {
  const [usage, setUsage] = useState<AdvocateUsageDto | null>(null);
  const [token, setToken] = useState(0);

  useEffect(() => {
    let active = true;
    getAdvocateUsage()
      .then((res) => {
        if (active && res.success && res.data) setUsage(res.data);
      })
      .catch(() => {
        // Non-critical — see the doc comment.
      });
    return () => {
      active = false;
    };
  }, [token]);

  const reload = useCallback(() => setToken((t) => t + 1), []);

  /** Local bump so the composer locks the moment the server refuses on the cap. */
  const markCapped = useCallback(() => {
    setUsage((u) => (u ? { ...u, used: Math.max(u.used, u.limit) } : u));
  }, []);

  return { usage, reload, markCapped };
}
