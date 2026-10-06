import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import i18n from './index';
import { isSupportedLanguage, setPreLoginLanguage } from './detect';

/**
 * Public pages honor a `?lang=en|es` query param once: it switches the
 * active language immediately and is remembered as the pre-login choice
 * (e.g. an invite link sent in the sender's language, per the design doc's
 * email handling), without touching a signed-in user's saved preference —
 * `AuthProvider` applies that separately and takes priority on the next
 * `/me` refresh.
 */
export function useLanguageQueryParam(): void {
  const [searchParams] = useSearchParams();
  const requested = searchParams.get('lang');

  useEffect(() => {
    if (!isSupportedLanguage(requested)) return;
    setPreLoginLanguage(requested);
    if (i18n.language !== requested) {
      void i18n.changeLanguage(requested);
    }
  }, [requested]);
}
