import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { Logo } from '@/components/ui/logo';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { apiErrorMessage } from '@/lib/api-error';
import { usePageTitle } from '@/hooks/use-page-title';
import { cancelDeletionByToken } from '../api/auth-api';

type Phase = 'loading' | 'success' | 'error';

const DEFAULT_ERROR = 'This link is invalid or has expired.';

/**
 * `/account/cancel-deletion?token=` — public, unauthenticated (pilot-gates
 * plan, phase 2). The requesting account is deactivated the moment a
 * deletion is scheduled, so this signed link — emailed at request time — is
 * the only reachable way to cancel; the token is submitted exactly once per
 * mount (`submittedRef`, guarding React StrictMode's double-invoked effect).
 */
export function CancelDeletionPage() {
  usePageTitle('Cancel account deletion');
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const [phase, setPhase] = useState<Phase>(token ? 'loading' : 'error');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  // Same shape as MagicLinkConsumePage: submit exactly once, track "mounted" with a ref every
  // effect invocation re-arms (StrictMode-safe), and scrub the token from the address bar.
  const submittedRef = useRef(false);
  const mountedRef = useRef(true);
  const tokenRef = useRef(token);

  useEffect(() => {
    mountedRef.current = true;
    const currentToken = tokenRef.current;
    if (!currentToken || submittedRef.current) {
      return () => {
        mountedRef.current = false;
      };
    }
    submittedRef.current = true;
    if (window.location.search.includes('token=')) {
      window.history.replaceState(null, '', window.location.pathname);
    }

    (async () => {
      try {
        const response = await cancelDeletionByToken(currentToken);
        if (!mountedRef.current) return;
        if (response.success) {
          setPhase('success');
        } else {
          setPhase('error');
          setErrorMessage(response.message ?? DEFAULT_ERROR);
        }
      } catch (err) {
        if (mountedRef.current) {
          setPhase('error');
          setErrorMessage(apiErrorMessage(err, DEFAULT_ERROR));
        }
      }
    })();

    return () => {
      mountedRef.current = false;
    };
  }, []);

  return (
    <div className="flex min-h-screen items-center justify-center bg-brand-slate-50 px-4 py-12">
      <Card className="w-full max-w-md text-center" data-testid="cancel-deletion-card">
        <div className="mb-4 flex justify-center">
          <Logo />
        </div>

        {phase === 'loading' && (
          <div className="flex justify-center py-4" data-testid="cancel-deletion-loading">
            <Spinner label="Cancelling your deletion request…" />
          </div>
        )}

        {phase === 'success' && (
          <div role="status">
            <Notice
              variant="success"
              title="Your deletion request was cancelled — you can sign in again"
              data-testid="cancel-deletion-success"
            />
          </div>
        )}

        {phase === 'error' && (
          <div role="alert">
            <Notice variant="error" title={errorMessage ?? DEFAULT_ERROR} data-testid="cancel-deletion-error" />
          </div>
        )}

        {phase !== 'loading' && (
          <div className="mt-4">
            <Link
              to="/login"
              className="text-sm text-brand-teal-500 hover:text-brand-teal-600"
              data-testid="cancel-deletion-login-link"
            >
              Go to sign in
            </Link>
          </div>
        )}
      </Card>
    </div>
  );
}
