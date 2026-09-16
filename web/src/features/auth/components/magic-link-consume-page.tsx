import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { usePageTitle } from '@/hooks/use-page-title';
import { useAuth } from '../hooks/use-auth';
import { consumeMagicLink } from '../api/auth-api';
import { MagicLinkRequestForm } from './magic-link-request-form';

const DEFAULT_MFA_SETUP_MESSAGE =
  'This district requires multi-factor authentication. Please sign in with your password to finish setting it up.';

type Phase = 'loading' | 'error' | 'mfa-setup-required';

/**
 * `/auth/magic?token=` — consumes a staff sign-in link (pilot-gates plan,
 * phase 3). The token is single-use, so consumption is attempted exactly
 * once per mount (`consumedRef`, guarding against React StrictMode's
 * double-invoked effect in dev — a second consume of an already-used token
 * would otherwise land as a spurious failure).
 *
 * On an outright success, stores the session exactly like the password login
 * flow (`applySession`, the same single source of truth used by
 * staff-invite acceptance and district signup) and routes to `/dashboard`;
 * an existing-MFA challenge routes to `/mfa-verify` with the pending token,
 * same as login. A district that requires MFA enrollment before magic-link
 * sign-in works is a distinct *refusal* (`mfaSetupRequired`) — no session is
 * issued — so it gets its own guidance state pointing at password sign-in,
 * not the "request a new link" recovery offered for a genuinely
 * invalid/expired token (`AuthController.ConsumeMagicLink` is authoritative
 * for this branching — it does not fit the login endpoint's own shape
 * one-for-one).
 */
export function MagicLinkConsumePage() {
  usePageTitle('Signing you in');
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const navigate = useNavigate();
  const { applySession } = useAuth();
  const [phase, setPhase] = useState<Phase>(token ? 'loading' : 'error');
  const [mfaSetupMessage, setMfaSetupMessage] = useState(DEFAULT_MFA_SETUP_MESSAGE);
  const [showRequestForm, setShowRequestForm] = useState(false);
  // Start the single-use POST exactly once (StrictMode double-invokes effects in dev), but track
  // "still mounted" with a ref that EVERY invocation re-arms — a per-invocation local would be
  // zeroed by StrictMode's synthetic cleanup and the one real response would then be discarded,
  // leaving the page on the spinner while the backend had already consumed the token.
  const consumedRef = useRef(false);
  const mountedRef = useRef(true);
  // The token is read once and then removed from the address bar (and from history), so it
  // never reaches Sentry breadcrumbs, browser history, or a shoulder-surfer.
  const tokenRef = useRef(token);

  useEffect(() => {
    mountedRef.current = true;
    const currentToken = tokenRef.current;
    if (!currentToken || consumedRef.current) {
      return () => {
        mountedRef.current = false;
      };
    }
    consumedRef.current = true;
    if (window.location.search.includes('token=')) {
      window.history.replaceState(null, '', window.location.pathname);
    }

    (async () => {
      try {
        const response = await consumeMagicLink(currentToken);
        if (!mountedRef.current) return;
        const data = response.data;

        if (data?.mfaSetupRequired) {
          setMfaSetupMessage(response.message ?? DEFAULT_MFA_SETUP_MESSAGE);
          setPhase('mfa-setup-required');
          return;
        }

        if (data?.requiresMfa && data.mfaPendingToken) {
          navigate('/mfa-verify', { state: { mfaPendingToken: data.mfaPendingToken } });
          return;
        }

        if (data?.token && data.user) {
          applySession(data.token, data.user);
          navigate('/dashboard');
          return;
        }

        setPhase('error');
      } catch {
        // An invalid/expired token answers 400 (see `consumeMagicLink`), which
        // lands here — never revealing more than "invalid or expired".
        if (mountedRef.current) setPhase('error');
      }
    })();

    return () => {
      mountedRef.current = false;
    };
  }, [navigate, applySession]);

  if (phase === 'loading') {
    return (
      <div className="flex justify-center py-6" data-testid="magic-consume-loading">
        <Spinner label="Signing you in…" />
      </div>
    );
  }

  if (phase === 'mfa-setup-required') {
    return (
      <div className="w-full text-center">
        <h2 className="text-2xl font-serif font-semibold mb-4 text-brand-slate-800">Sign-in link</h2>
        <div className="mb-4">
          <div role="status">
            <Notice variant="info" title={mfaSetupMessage} data-testid="magic-consume-mfa-setup" />
          </div>
        </div>
        <Link to="/login" className="text-sm text-brand-teal-500 hover:text-brand-teal-600" data-testid="magic-consume-go-password">
          Sign in with your password
        </Link>
      </div>
    );
  }

  return (
    <div className="w-full text-center">
      <h2 className="text-2xl font-serif font-semibold mb-4 text-brand-slate-800">Sign-in link</h2>
      <div className="mb-4">
        <div role="alert">
          <Notice variant="error" title="This link is invalid or has expired" data-testid="magic-consume-error" />
        </div>
      </div>
      {showRequestForm ? (
        <MagicLinkRequestForm />
      ) : (
        <Button onClick={() => setShowRequestForm(true)} data-testid="magic-consume-request-new">
          Request a new link
        </Button>
      )}
    </div>
  );
}
