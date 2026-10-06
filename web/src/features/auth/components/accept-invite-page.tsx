import { useEffect, useState } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { acceptInvite } from '@/features/sharing/api/sharing-api';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { usePageTitle } from '@/hooks/use-page-title';

type ErrorReason = 'noToken' | 'serverFailed' | 'clientError';

export function AcceptInvitePage() {
  const { t } = useTranslation('auth');
  usePageTitle(t('acceptInvite.pageTitle'));
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const [status, setStatus] = useState<'loading' | 'success' | 'error'>('loading');
  // The reason (not a pre-localized string) is stored, and resolved to text at
  // render time via `t()` — so a language switch while this is showing an
  // error updates the text immediately, without re-running the effect (and
  // re-POSTing `acceptInvite`) just because `t`'s identity changed.
  const [errorReason, setErrorReason] = useState<ErrorReason | null>(null);
  const [serverMessage, setServerMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      setStatus('error');
      setErrorReason('noToken');
      return;
    }

    async function accept() {
      try {
        const response = await acceptInvite(token!);
        if (response.success) {
          setStatus('success');
        } else {
          setStatus('error');
          setErrorReason('serverFailed');
          setServerMessage(response.message || null);
        }
      } catch {
        setStatus('error');
        setErrorReason('clientError');
      }
    }

    accept();
  }, [token]);

  const errorMessage =
    serverMessage ??
    (errorReason === 'noToken'
      ? t('acceptInvite.noToken')
      : errorReason === 'clientError'
        ? t('acceptInvite.error')
        : t('acceptInvite.failed'));

  return (
    <div className="max-w-md mx-auto py-12">
      <Card className="text-center">
        <h1 className="font-serif mb-4">{t('acceptInvite.title')}</h1>

        {status === 'loading' && (
          <div className="flex justify-center py-6">
            <Spinner label={t('acceptInvite.accepting')} />
          </div>
        )}

        {status === 'success' && (
          <div className="space-y-4">
            <Notice variant="success" title={t('acceptInvite.successTitle')}>
              {t('acceptInvite.successDetail')}
            </Notice>
            <Link to="/dashboard">
              <Button>{t('acceptInvite.goToDashboard')}</Button>
            </Link>
          </div>
        )}

        {status === 'error' && (
          <div className="space-y-4">
            <Notice variant="error" title={errorMessage} />
            <Link to="/dashboard">
              <Button variant="secondary">{t('acceptInvite.goToDashboard')}</Button>
            </Link>
          </div>
        )}
      </Card>
    </div>
  );
}
