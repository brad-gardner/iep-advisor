import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/use-auth';
import { MagicLinkRequestForm } from './magic-link-request-form';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { usePageTitle } from '@/hooks/use-page-title';

export function LoginPage() {
  usePageTitle('Sign in');
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useAuth();
  const successMessage = (location.state as { message?: string })?.message;
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  // Staff (RelatedServiceProvider/GeneralEducator) can sign in via a 15-minute
  // emailed link instead of a password (pilot-gates plan, phase 3). Toggling
  // this swaps the password form out for the small email-only one.
  const [showMagicLink, setShowMagicLink] = useState(false);
  // Toggling replaces the form under the user's focus; move focus to the panel that
  // appeared so keyboard and screen-reader users land on the new content, not <body>.
  const panelRef = useRef<HTMLDivElement>(null);
  const passwordPanelRef = useRef<HTMLDivElement>(null);
  const toggledRef = useRef(false);
  useEffect(() => {
    if (!toggledRef.current) return; // never steal focus on the initial render
    (showMagicLink ? panelRef.current : passwordPanelRef.current)?.focus();
  }, [showMagicLink]);
  const showMagicLinkRef = useRef(false);
  const toggleMagicLink = (next: boolean) => {
    toggledRef.current = true;
    showMagicLinkRef.current = next;
    setError(''); // a delayed failure from the abandoned form must not surface over the other one
    setShowMagicLink(next);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      const result = await login({ email: email.trim(), password });

      if (result.success) {
        navigate('/dashboard');
      } else if (result.requiresMfa && result.mfaPendingToken) {
        navigate('/mfa-verify', { state: { mfaPendingToken: result.mfaPendingToken } });
      } else if (!showMagicLinkRef.current) {
        // (a failure that lands after the user moved to the magic-link panel is dropped)
        setError(result.error || 'Login failed');
      }
    } finally {
      setIsLoading(false); // on every path — a stuck `loading` would disable Sign In until a reload
    }
  };

  return (
    <div className="w-full">
      <h2 className="text-2xl font-serif font-semibold text-center mb-6 text-brand-slate-800">Welcome Back</h2>

      {successMessage && <div className="mb-4" data-testid="login-success-message"><Notice variant="success" title={successMessage} /></div>}
      {error && <div className="mb-4" data-testid="login-error"><Notice variant="error" title={error} /></div>}

      {showMagicLink ? (
        <div data-testid="login-magic-link-panel" ref={panelRef} tabIndex={-1} className="rounded-card focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500">
          <MagicLinkRequestForm />
          <div className="mt-4 text-center">
            <button
              type="button"
              onClick={() => toggleMagicLink(false)}
              className="text-xs text-brand-slate-500 hover:text-brand-slate-600"
              data-testid="magic-link-back"
            >
              Back to password sign-in
            </button>
          </div>
        </div>
      ) : (
        <div ref={passwordPanelRef} tabIndex={-1} className="rounded-card focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500" data-testid="login-password-panel">
          <form onSubmit={handleSubmit} className="space-y-4" data-testid="login-form">
            <Input
              label="Email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              placeholder="you@example.com"
              maxLength={256}
              data-testid="login-email"
            />

            <div>
              <Input
                label="Password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                placeholder="********"
                maxLength={128}
                data-testid="login-password"
              />
              <div className="mt-1 text-right">
                <Link
                  to="/forgot-password"
                  className="text-xs text-brand-teal-500 hover:text-brand-teal-600"
                  data-testid="forgot-password-link"
                >
                  Forgot password?
                </Link>
              </div>
            </div>

            <Button type="submit" loading={isLoading} className="w-full" data-testid="login-submit">
              Sign In
            </Button>
          </form>

          <div className="mt-4 text-center">
            <button
              type="button"
              onClick={() => toggleMagicLink(true)}
              className="text-xs text-brand-teal-500 hover:text-brand-teal-600"
              data-testid="magic-link-toggle"
            >
              Email me a sign-in link
            </button>
          </div>
        </div>
      )}

      <p className="mt-6 text-center text-sm text-brand-slate-500">
        Don't have an account?{' '}
        <Link to="/register" className="text-brand-teal-500 hover:text-brand-teal-600" data-testid="register-link">
          Sign up
        </Link>
      </p>
    </div>
  );
}
