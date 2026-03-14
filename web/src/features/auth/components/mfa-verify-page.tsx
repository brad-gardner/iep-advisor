import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/use-auth';
import { verifyMfa, mfaRecovery } from '../api/auth-api';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Logo } from '@/components/ui/logo';

export function MfaVerifyPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { mfaPendingToken: contextToken, completeMfaLogin } = useAuth();

  const mfaPendingToken = (location.state as { mfaPendingToken?: string })?.mfaPendingToken ?? contextToken;

  const [code, setCode] = useState('');
  const [recoveryCode, setRecoveryCode] = useState('');
  const [useRecovery, setUseRecovery] = useState(false);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleVerify = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!mfaPendingToken) {
      setError('Missing authentication token. Please log in again.');
      return;
    }

    setError('');
    setIsLoading(true);

    try {
      const response = useRecovery
        ? await mfaRecovery(mfaPendingToken, recoveryCode.trim())
        : await verifyMfa(mfaPendingToken, code.trim());

      if (response.success && response.data?.token && response.data?.user) {
        completeMfaLogin(response.data.token, response.data.user);
        navigate('/dashboard', { replace: true });
      } else {
        setError(response.message || 'Verification failed');
      }
    } catch {
      setError('An error occurred during verification');
    } finally {
      setIsLoading(false);
    }
  };

  if (!mfaPendingToken) {
    return (
      <div className="min-h-screen bg-brand-slate-50 flex items-center justify-center p-4">
        <Card className="w-full max-w-md text-center">
          <Notice variant="error" title="Session expired">
            <p className="mt-1">Please log in again to continue.</p>
          </Notice>
          <Button onClick={() => navigate('/login')} className="mt-4">
            Back to Login
          </Button>
        </Card>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-brand-slate-50 flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        <div className="flex justify-center mb-6">
          <Logo />
        </div>

        <Card>
          <h2 className="text-xl font-serif font-semibold text-center mb-2 text-brand-slate-800">
            Two-Factor Authentication
          </h2>
          <p className="text-sm text-brand-slate-400 text-center mb-6">
            {useRecovery
              ? 'Enter one of your recovery codes'
              : 'Enter the 6-digit code from your authenticator app'}
          </p>

          {error && (
            <div className="mb-4">
              <Notice variant="error" title={error} />
            </div>
          )}

          <form onSubmit={handleVerify} className="space-y-4">
            {useRecovery ? (
              <Input
                label="Recovery Code"
                type="text"
                value={recoveryCode}
                onChange={(e) => setRecoveryCode(e.target.value)}
                required
                placeholder="XXXX-XXXX-XXXX"
                autoFocus
              />
            ) : (
              <Input
                label="Verification Code"
                type="text"
                inputMode="numeric"
                value={code}
                onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                required
                placeholder="000000"
                maxLength={6}
                autoFocus
              />
            )}

            <Button type="submit" disabled={isLoading} className="w-full">
              {isLoading ? 'Verifying...' : 'Verify'}
            </Button>
          </form>

          <div className="mt-4 text-center">
            <button
              type="button"
              onClick={() => {
                setUseRecovery(!useRecovery);
                setError('');
                setCode('');
                setRecoveryCode('');
              }}
              className="text-xs text-brand-teal-500 hover:text-brand-teal-600"
            >
              {useRecovery ? 'Use authenticator code instead' : 'Use a recovery code'}
            </button>
          </div>

          <div className="mt-2 text-center">
            <button
              type="button"
              onClick={() => navigate('/login')}
              className="text-xs text-brand-slate-400 hover:text-brand-slate-600"
            >
              Back to login
            </button>
          </div>
        </Card>
      </div>
    </div>
  );
}
