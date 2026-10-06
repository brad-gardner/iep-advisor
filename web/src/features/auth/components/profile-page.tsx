import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../hooks/use-auth';
import { disableMfa } from '../api/auth-api';
import { StateSelector } from './state-selector';
import { AccountDeletionSection } from './account-deletion-section';
import { SubscriptionStatusCard } from '@/features/subscription/components/subscription-status';
import { CalendarSubscriptionCard } from '@/features/calendar/components/calendar-subscription-card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { LanguageSwitcher } from '@/lib/i18n/language-switcher';
import { usePageTitle } from '@/hooks/use-page-title';

function MfaSection() {
  const { t } = useTranslation('auth');
  const { user } = useAuth();
  const [showDisable, setShowDisable] = useState(false);
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [disableSuccess, setDisableSuccess] = useState(false);

  const handleDisable = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);
    try {
      const response = await disableMfa(password, code.trim());
      if (response.success) {
        setDisableSuccess(true);
        setShowDisable(false);
      } else {
        setError(response.message || t('mfaSection.failed'));
      }
    } catch {
      setError(t('mfaSection.error'));
    } finally {
      setIsLoading(false);
    }
  };

  const isMfaEnabled = user?.mfaEnabled && !disableSuccess;

  return (
    <div className="space-y-3">
      {disableSuccess && (
        <Notice variant="success" title={t('mfaSection.disabled')} />
      )}

      {isMfaEnabled ? (
        <>
          <div className="flex items-center gap-2">
            <Badge variant="success">{t('mfaSection.enabledBadge')}</Badge>
          </div>

          {!showDisable ? (
            <Button variant="ghost" onClick={() => setShowDisable(true)}>
              {t('mfaSection.disableButton')}
            </Button>
          ) : (
            <div className="border border-brand-slate-100 rounded-card p-4">
              {error && (
                <div className="mb-3">
                  <Notice variant="error" title={error} />
                </div>
              )}
              <form onSubmit={handleDisable} className="space-y-3">
                <Input
                  label={t('fields.password')}
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  placeholder={t('mfaSection.passwordPlaceholder')}
                />
                <Input
                  label={t('mfaSection.authenticatorCode')}
                  type="text"
                  inputMode="numeric"
                  value={code}
                  onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  required
                  placeholder={t('fields.codePlaceholder')}
                  maxLength={6}
                />
                <div className="flex gap-3">
                  <Button type="submit" loading={isLoading}>
                    {t('mfaSection.confirmDisable')}
                  </Button>
                  <Button
                    variant="ghost"
                    type="button"
                    onClick={() => {
                      setShowDisable(false);
                      setPassword('');
                      setCode('');
                      setError('');
                    }}
                  >
                    {t('mfaSection.cancel')}
                  </Button>
                </div>
              </form>
            </div>
          )}
        </>
      ) : (
        <div>
          <p className="text-sm text-brand-slate-500 mb-2">
            {t('mfaSection.enableDescription')}
          </p>
          <Link to="/mfa-setup">
            <Button variant="secondary">{t('mfaSection.enableButton')}</Button>
          </Link>
        </div>
      )}
    </div>
  );
}

export function ProfilePage() {
  const { t } = useTranslation('auth');
  usePageTitle(t('profile.pageTitle'));
  const { user, updateProfile } = useAuth();
  const [firstName, setFirstName] = useState(user?.firstName ?? '');
  const [lastName, setLastName] = useState(user?.lastName ?? '');
  const [state, setState] = useState(user?.state ?? '');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setMessage(null);

    const result = await updateProfile({
      firstName: firstName.trim(),
      lastName: lastName.trim(),
      state: state || undefined,
    });

    if (result.success) {
      setMessage({ type: 'success', text: t('profile.saveSuccess') });
    } else {
      setMessage({ type: 'error', text: result.error ?? t('profile.saveFailed') });
    }

    setIsSubmitting(false);
  };

  return (
    <PageLayout title={t('profile.pageTitle')}>
      <Card className="max-w-lg">
        <form onSubmit={handleSubmit} className="space-y-4">
          {message && (
            <Notice
              variant={message.type === 'success' ? 'success' : 'error'}
              title={message.text}
            />
          )}

          <Input
            label={t('fields.email')}
            type="text"
            value={user?.email ?? ''}
            disabled
            className="bg-brand-slate-50 text-brand-slate-500 cursor-not-allowed"
            data-testid="profile-email"
          />

          <Input
            label={t('fields.firstName')}
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            maxLength={100}
            data-testid="profile-first-name"
          />

          <Input
            label={t('fields.lastName')}
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
            maxLength={100}
            data-testid="profile-last-name"
          />

          <div>
            <label htmlFor="state" className="block text-[13px] font-medium text-brand-slate-600 mb-1">
              {t('profile.stateLabel')}
            </label>
            <StateSelector value={state} onChange={setState} />
            <p className="text-[11px] text-brand-slate-500 mt-1">
              {t('profile.stateHint')}
            </p>
          </div>

          <Button type="submit" loading={isSubmitting} disabled={!firstName.trim()} className="w-full" data-testid="profile-save">
            {t('profile.submit')}
          </Button>
        </form>
      </Card>

      <Card className="max-w-lg" data-testid="profile-language-section">
        <label className="block text-[13px] font-medium text-brand-slate-600 mb-1">
          {t('profile.languageLabel')}
        </label>
        <LanguageSwitcher tone="onLight" data-testid="profile-language-switcher" />
        <p className="text-[11px] text-brand-slate-500 mt-2">{t('profile.languageHint')}</p>
      </Card>

      <div className="max-w-lg space-y-3">
        <SubscriptionStatusCard />
        <Link
          to="/redeem-invite"
          className="inline-block text-sm text-brand-teal-500 hover:text-brand-teal-600 underline"
        >
          {t('profile.redeemInvite')}
        </Link>
      </div>

      <CalendarSubscriptionCard />

      <Card className="max-w-lg" data-testid="mfa-section">
        <h2 className="text-lg font-serif font-semibold text-brand-slate-800 mb-4">
          {t('profile.mfaTitle')}
        </h2>
        <MfaSection />
      </Card>

      <Card className="max-w-lg" data-testid="account-section">
        <h2 className="text-lg font-serif font-semibold text-brand-slate-800 mb-4">
          {t('profile.accountTitle')}
        </h2>
        <AccountDeletionSection />
      </Card>
    </PageLayout>
  );
}
