import { useEffect, useState } from 'react';
import { getAdvocateChildContext } from '../api/advocate-api';

/**
 * The state the advocate will actually apply for this child, resolved server-side
 * (linked district → school → the parent's own profile). The "set your state" hint
 * keys off this rather than the parent's profile alone: a child linked to an Ohio
 * district gets Ohio rules even when the parent never filled in their profile.
 * `undefined` while loading (no hint yet); `null` when nothing resolved.
 */
export function useAdvocateChildContext(childId: number) {
  // Keyed by child so a switch shows "loading" (undefined) again without a reset-in-effect.
  const [resolved, setResolved] = useState<{ childId: number; stateCode: string | null } | null>(null);

  useEffect(() => {
    let active = true;
    getAdvocateChildContext(childId)
      .then((res) => {
        // Only a genuine server answer counts as "resolved" — a failed
        // response is not the same as the server positively saying "no
        // state", so it must not flip to `null` and trigger the hint for a
        // parent whose child's state simply failed to load this time.
        if (active && res.success && res.data) setResolved({ childId, stateCode: res.data.stateCode });
      })
      .catch(() => {
        // Network/exception: leave `resolved` as-is (stays `undefined` for
        // this child) rather than reporting a positive "no state".
      });
    return () => {
      active = false;
    };
  }, [childId]);

  return resolved?.childId === childId ? resolved.stateCode : undefined;
}
