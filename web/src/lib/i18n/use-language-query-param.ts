import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import i18n from './index';
import { getToken } from '@/lib/auth';
import { isSupportedLanguage, setPreLoginLanguage } from './detect';

/**
 * Public pages honor a `?lang=en|es` query param once: it switches the
 * active language immediately for DISPLAY, and — only while actually
 * signed out — is remembered as the pre-login choice (e.g. an invite link
 * sent in the sender's language, per the design doc's email handling).
 *
 * A signed-in visitor following the same kind of link (e.g. a share invite
 * opened while already signed in as someone else) still sees the requested
 * language, but it must never be written to the pre-login key: that key may
 * only ever hold something a not-yet-signed-in VISITOR chose (see the module
 * doc comment on `lib/i18n/detect.ts`), and writing it here would let a
 * signed-in account's incidental link click silently decide a DIFFERENT,
 * later visitor's backfill on a shared device. `AuthProvider` is the only
 * thing that ever persists a signed-in user's own choice, and takes priority
 * on the next `/me` refresh.
 */
export function useLanguageQueryParam(): void {
  const [searchParams] = useSearchParams();
  const requested = searchParams.get('lang');

  useEffect(() => {
    if (!isSupportedLanguage(requested)) return;
    if (!getToken()) {
      setPreLoginLanguage(requested);
    }
    if (i18n.language !== requested) {
      void i18n.changeLanguage(requested);
    }
  }, [requested]);
}
