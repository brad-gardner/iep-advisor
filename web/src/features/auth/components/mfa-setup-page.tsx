import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { QRCodeSVG } from 'qrcode.react';
import { useTranslation } from 'react-i18next';
import { setupMfa, verifyMfaSetup } from '../api/auth-api';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';

import { usePageTitle } from '@/hooks/use-page-title';

type SetupStep = 'qr' | 'verify' | 'recovery';

export function MfaSetupPage() {
  const { t } = useTranslation('auth');
  usePageTitle(t('mfaSetup.pageTitle'));
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
        setError(response.message || t('mfaSetup.setupFailed'));
      }
    } catch {
      setError(t('mfaSetup.setupError'));
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
        setError(response.message || t('mfaSetup.invalidCode'));
      }
    } catch {
      setError(t('mfaSetup.verifyError'));
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
        <h1 className="font-serif">{t('mfaSetup.enableTitle')}</h1>
        <Card className="max-w-lg">
          <p className="text-sm text-brand-slate-600 mb-4">
            {t('mfaSetup.enableDescription')}
          </p>
          {error && (
            <div className="mb-4">
              <Notice variant="error" title={error} />
            </div>
          )}
          <div className="flex gap-3">
            <Button onClick={handleSetup} loading={isLoading}>
              {t('mfaSetup.getStarted')}
            </Button>
            <Button variant="ghost" onClick={() => navigate('/profile')}>
              {t('mfaSetup.cancel')}
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
        <h1 className="font-serif">{t('mfaSetup.scanTitle')}</h1>
        <Card className="max-w-lg">
          <p className="text-sm text-brand-slate-600 mb-4">
            {t('mfaSetup.scanDescription')}
          </p>

          <div className="flex justify-center mb-4 p-4 bg-white rounded-card border border-brand-slate-100">
            <QRCodeSVG value={otpauthUri} size={200} />
          </div>

          <div className="mb-4">
            <p className="text-xs text-brand-slate-500 mb-1">{t('mfaSetup.manualEntryPrompt')}</p>
            <code className="block text-sm bg-brand-slate-50 border border-brand-slate-100 rounded-card px-3 py-2 font-mono text-brand-slate-700 break-all select-all">
              {manualEntryKey}
            </code>
          </div>

          <Button onClick={() => setStep('verify')} className="w-full">
            {t('mfaSetup.continue')}
          </Button>
        </Card>
      </div>
    );
  }

  // Step 2: Verify code
  if (step === 'verify') {
    return (
      <div className="space-y-6">
        <h1 className="font-serif">{t('mfaSetup.verifyTitle')}</h1>
        <Card className="max-w-lg">
          <p className="text-sm text-brand-slate-600 mb-4">
            {t('mfaSetup.verifyDescription')}
          </p>

          {error && (
            <div className="mb-4">
              <Notice variant="error" title={error} />
            </div>
          )}

          <form onSubmit={handleVerify} className="space-y-4">
            <Input
              label={t('fields.verificationCode')}
              type="text"
              inputMode="numeric"
              value={code}
              onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
              required
              placeholder={t('fields.codePlaceholder')}
              maxLength={6}
              autoFocus
            />

            <div className="flex gap-3">
              <Button type="submit" loading={isLoading} className="flex-1">
                {t('mfaSetup.verifyAndEnable')}
              </Button>
              <Button variant="ghost" type="button" onClick={() => setStep('qr')}>
                {t('mfaSetup.back')}
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
      <h1 className="font-serif">{t('mfaSetup.recoveryTitle')}</h1>
      <Card className="max-w-lg">
        <Notice variant="warning" title={t('mfaSetup.recoveryWarningTitle')}>
          <p className="mt-1">
            {t('mfaSetup.recoveryWarningDetail')}
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
            {copied ? t('mfaSetup.copied') : t('mfaSetup.copyAll')}
          </Button>
          <Button onClick={() => navigate('/profile')}>
            {t('mfaSetup.done')}
          </Button>
        </div>
      </Card>
    </div>
  );
}
