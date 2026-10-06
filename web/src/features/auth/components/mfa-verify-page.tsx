import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../hooks/use-auth';
import { verifyMfa, mfaRecovery } from '../api/auth-api';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Logo } from '@/components/ui/logo';
import { usePageTitle } from '@/hooks/use-page-title';

export function MfaVerifyPage() {
  const { t } = useTranslation('auth');
  usePageTitle(t('mfaVerify.pageTitle'));
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
      setError(t('mfaVerify.missingToken'));
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
        setError(response.message || t('mfaVerify.failed'));
      }
    } catch {
      setError(t('mfaVerify.error'));
    } finally {
      setIsLoading(false);
    }
  };

  if (!mfaPendingToken) {
    return (
      <div className="min-h-screen bg-brand-slate-50 flex items-center justify-center p-4">
        <Card className="w-full max-w-md text-center">
          <Notice variant="error" title={t('mfaVerify.sessionExpiredTitle')}>
            <p className="mt-1">{t('mfaVerify.sessionExpiredDetail')}</p>
          </Notice>
          <Button onClick={() => navigate('/login')} className="mt-4">
            {t('mfaVerify.backToLoginButton')}
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
            {t('mfaVerify.title')}
          </h2>
          <p className="text-sm text-brand-slate-500 text-center mb-6">
            {useRecovery
              ? t('mfaVerify.enterRecoveryCode')
              : t('mfaVerify.enterAuthCode')}
          </p>

          {error && (
            <div className="mb-4">
              <Notice variant="error" title={error} />
            </div>
          )}

          <form onSubmit={handleVerify} className="space-y-4">
            {useRecovery ? (
              <Input
                label={t('mfaVerify.recoveryCode')}
                type="text"
                value={recoveryCode}
                onChange={(e) => setRecoveryCode(e.target.value)}
                required
                placeholder={t('mfaVerify.recoveryCodePlaceholder')}
                autoFocus
              />
            ) : (
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
            )}

            <Button type="submit" loading={isLoading} className="w-full">
              {t('mfaVerify.submit')}
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
              {useRecovery ? t('mfaVerify.useAuthenticatorCode') : t('mfaVerify.useRecoveryCode')}
            </button>
          </div>

          <div className="mt-2 text-center">
            <button
              type="button"
              onClick={() => navigate('/login')}
              className="text-xs text-brand-slate-500 hover:text-brand-slate-600"
            >
              {t('mfaVerify.backToLogin')}
            </button>
          </div>
        </Card>
      </div>
    </div>
  );
}
