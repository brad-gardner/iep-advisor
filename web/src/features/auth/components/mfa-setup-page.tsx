import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { QRCodeSVG } from 'qrcode.react';
import { setupMfa, verifyMfaSetup } from '../api/auth-api';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';

type SetupStep = 'qr' | 'verify' | 'recovery';

export function MfaSetupPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState<SetupStep>('qr');
  const [otpauthUri, setOtpauthUri] = useState('');
  const [manualEntryKey, setManualEntryKey] = useState('');
  const [code, setCode] = useState('');
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [copied, setCopied] = useState(false);

  const handleSetup = async () => {
    setError('');
    setIsLoading(true);
    try {
      const response = await setupMfa();
      if (response.success && response.data) {
        setOtpauthUri(response.data.otpauthUri);
        setManualEntryKey(response.data.manualEntryKey);
      } else {
        setError(response.message || 'Failed to start MFA setup');
      }
    } catch {
      setError('An error occurred starting MFA setup');
    } finally {
      setIsLoading(false);
    }
  };

  const handleVerify = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);
    try {
      const response = await verifyMfaSetup(code.trim());
      if (response.success && response.data) {
        setRecoveryCodes(response.data.recoveryCodes);
        setStep('recovery');
      } else {
        setError(response.message || 'Invalid code. Please try again.');
      }
    } catch {
      setError('An error occurred during verification');
    } finally {
      setIsLoading(false);
    }
  };

  const handleCopyAll = async () => {
    try {
      await navigator.clipboard.writeText(recoveryCodes.join('\n'));
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback: select text for manual copy
    }
  };

  // Initial state: start setup
  if (!otpauthUri && step === 'qr') {
    return (
      <div className="space-y-6">
        <h1 className="font-serif">Enable Two-Factor Authentication</h1>
        <Card className="max-w-lg">
          <p className="text-sm text-brand-slate-600 mb-4">
            Add an extra layer of security to your account by requiring a verification code
            from an authenticator app when you sign in.
          </p>
          {error && (
            <div className="mb-4">
              <Notice variant="error" title={error} />
            </div>
          )}
          <div className="flex gap-3">
            <Button onClick={handleSetup} disabled={isLoading}>
              {isLoading ? 'Setting up...' : 'Get Started'}
            </Button>
            <Button variant="ghost" onClick={() => navigate('/profile')}>
              Cancel
            </Button>
          </div>
        </Card>
      </div>
    );
  }

  // Step 1: Show QR code
  if (step === 'qr') {
    return (
      <div className="space-y-6">
        <h1 className="font-serif">Scan QR Code</h1>
        <Card className="max-w-lg">
          <p className="text-sm text-brand-slate-600 mb-4">
            Scan this QR code with your authenticator app (Google Authenticator, Authy, etc.)
          </p>

          <div className="flex justify-center mb-4 p-4 bg-white rounded-card border border-brand-slate-100">
            <QRCodeSVG value={otpauthUri} size={200} />
          </div>

          <div className="mb-4">
            <p className="text-xs text-brand-slate-400 mb-1">Can't scan? Enter this key manually:</p>
            <code className="block text-sm bg-brand-slate-50 border border-brand-slate-100 rounded-card px-3 py-2 font-mono text-brand-slate-700 break-all select-all">
              {manualEntryKey}
            </code>
          </div>

          <Button onClick={() => setStep('verify')} className="w-full">
            Continue
          </Button>
        </Card>
      </div>
    );
  }

  // Step 2: Verify code
  if (step === 'verify') {
    return (
      <div className="space-y-6">
        <h1 className="font-serif">Verify Setup</h1>
        <Card className="max-w-lg">
          <p className="text-sm text-brand-slate-600 mb-4">
            Enter the 6-digit code from your authenticator app to confirm setup.
          </p>

          {error && (
            <div className="mb-4">
              <Notice variant="error" title={error} />
            </div>
          )}

          <form onSubmit={handleVerify} className="space-y-4">
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

            <div className="flex gap-3">
              <Button type="submit" disabled={isLoading} className="flex-1">
                {isLoading ? 'Verifying...' : 'Verify & Enable'}
              </Button>
              <Button variant="ghost" type="button" onClick={() => setStep('qr')}>
                Back
              </Button>
            </div>
          </form>
        </Card>
      </div>
    );
  }

  // Step 3: Recovery codes
  return (
    <div className="space-y-6">
      <h1 className="font-serif">Recovery Codes</h1>
      <Card className="max-w-lg">
        <Notice variant="warning" title="Save these codes — you won't see them again">
          <p className="mt-1">
            If you lose access to your authenticator app, you can use one of these codes to sign in.
            Each code can only be used once.
          </p>
        </Notice>

        <div className="mt-4 bg-brand-slate-50 border border-brand-slate-100 rounded-card p-4">
          <ul className="grid grid-cols-2 gap-2">
            {recoveryCodes.map((rc) => (
              <li key={rc} className="font-mono text-sm text-brand-slate-700">
                {rc}
              </li>
            ))}
          </ul>
        </div>

        <div className="mt-4 flex gap-3">
          <Button variant="secondary" onClick={handleCopyAll}>
            {copied ? 'Copied!' : 'Copy All'}
          </Button>
          <Button onClick={() => navigate('/profile')}>
            Done
          </Button>
        </div>
      </Card>
    </div>
  );
}
