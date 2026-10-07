import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiErrorMessage } from '@/lib/api-error';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { getHome } from '../api/home-api';
import type { HomeDto } from '../types';

interface UseHomeResult {
  home: HomeDto | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

// A server-provided message is already resolved text (localized server-side,
// per the plan's `.resx` work) and is shown as-is; the generic fallback is
// translated at RENDER time instead of load time (see `useHome` below) so a
// language switch after a failed load shows the new language immediately,
// without needing to refetch.
type HomeLoadError = { kind: 'server'; message: string } | { kind: 'generic' };

/**
 * Loads the single role-scoped `GET /api/home` aggregate. A failure never
 * degrades to an empty state — callers render the error with a retry action.
 * Loading is *derived* (`home === null && error === null`), so no setState
 * runs synchronously inside the effect — same idiom as
 * `student-timeline-card.tsx`/`notifications-page.tsx`: a retry re-runs the
 * fetch behind the existing error until it resolves.
 */
export function useHome(): UseHomeResult {
  const { t, i18n } = useTranslation('home');
  const { user } = useAuth();
  const [home, setHome] = useState<HomeDto | null>(null);
  const [loadError, setLoadError] = useState<HomeLoadError | null>(null);
  // Bumped by the "Try again" button to re-run the load effect below.
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await getHome();
        if (!active) return;
        if (response.success && response.data) {
          setHome(response.data);
          setLoadError(null);
        } else {
          setLoadError(response.message ? { kind: 'server', message: response.message } : { kind: 'generic' });
        }
      } catch (err) {
        if (!active) return;
        // `apiErrorMessage` with no fallback (`''`) tells us ONLY whether the
        // server itself supplied a message — the generic text is filled in
        // below, at render, in whichever language is active then.
        const serverMessage = apiErrorMessage(err, '');
        setLoadError(serverMessage ? { kind: 'server', message: serverMessage } : { kind: 'generic' });
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded: re-running the fetch on a plain UI language
    // switch would be wasteful and racy (phase 2 review). The generic error
    // text is translated below, at render, from `loadError`'s stored KIND
    // rather than a snapshot string, so it already follows the active
    // language with no refetch needed.
    // `i18n.resolvedLanguage` AND `user?.preferredLanguage` are both
    // included, because the server picks between them itself: a signed-in
    // request is localized from the account's SAVED preference, not
    // `Accept-Language`, while a signed-out one has no saved preference to
    // read and falls back to `Accept-Language` (i.e. the language actually
    // in use). So this effect must refetch on whichever of the two actually
    // drives the response for the current viewer — `resolvedLanguage` for
    // the signed-out case (a lazy Spanish chunk finishing, or a switch that
    // reverted) and `user?.preferredLanguage` for the signed-in case (the
    // backfill/PUT in `AuthProvider` landing after this effect's first run).
    // `resolvedLanguage` changes only once a language switch has actually
    // taken effect (never mid-switch, and never on a switch that reverted),
    // so this still doesn't reintroduce the refetch-per-switch-attempt
    // problem `t` was excluded for above.
  }, [retryToken, i18n.resolvedLanguage, user?.preferredLanguage]);

  return {
    home,
    isLoading: home === null && loadError === null,
    error: loadError ? (loadError.kind === 'server' ? loadError.message : t('errors.loadFailed')) : null,
    retry: () => setRetryToken((n) => n + 1),
  };
}
