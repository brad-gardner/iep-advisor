import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Trans, useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/card';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { usePageTitle } from '@/hooks/use-page-title';
import { acceptInvite, previewInvite } from '../api/student-invite-api';
import type { StudentInvitePreviewDto } from '../types';

type Status = 'loading' | 'ready' | 'submitting' | 'error';
type ErrorReason = 'loadFailed' | 'loadError' | 'acceptFailed' | 'acceptError';

export function StudentAcceptInvitePage() {
  const { t } = useTranslation('auth');
  usePageTitle(t('studentAcceptInvite.pageTitle'));
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const navigate = useNavigate();
  const { refreshUser } = useAuth();

  const [status, setStatus] = useState<Status>('loading');
  // The reason (not a pre-localized string) is stored, and resolved to text
  // at render time via `t()` — so a language switch while an error is
  // showing updates the text immediately, without re-running an effect (and
  // re-fetching/re-submitting) just because `t`'s identity changed.
  const [errorReason, setErrorReason] = useState<ErrorReason | null>(null);
  const [serverMessage, setServerMessage] = useState<string | null>(null);
  const [preview, setPreview] = useState<StudentInvitePreviewDto | null>(null);
  const [consentAccepted, setConsentAccepted] = useState(false);

  useEffect(() => {
    if (!token) return;

    let active = true;
    async function load() {
      try {
        const response = await previewInvite(token!);
        if (!active) return;
        if (response.success && response.data) {
          setPreview(response.data);
          setStatus('ready');
        } else {
          setStatus('error');
          setErrorReason('loadFailed');
          setServerMessage(response.message || null);
        }
      } catch {
        if (active) {
          setStatus('error');
          setErrorReason('loadError');
        }
      }
    }

    load();
    return () => {
      active = false;
    };
  }, [token]);

  // Missing token is derived at render time (no setState-in-effect needed).
  const isMissingToken = !token;

  const handleAccept = async () => {
    if (!token || !consentAccepted) return;
    setStatus('submitting');
    setErrorReason(null);
    setServerMessage(null);

    try {
      const response = await acceptInvite(token, true);
      if (response.success && response.data) {
        // The accept converts the user to the Student role server-side; refresh
        // the cached user so routing/sidebar reflect the new role before nav.
        await refreshUser();
        navigate('/student', { replace: true });
      } else {
        setStatus('error');
        setErrorReason('acceptFailed');
        setServerMessage(response.message || null);
      }
    } catch {
      setStatus('error');
      setErrorReason('acceptError');
    }
  };

  function reasonMessage(reason: ErrorReason): string {
    switch (reason) {
      case 'loadFailed':
        return t('studentAcceptInvite.loadFailed');
      case 'loadError':
        return t('studentAcceptInvite.loadError');
      case 'acceptFailed':
        return t('studentAcceptInvite.acceptFailed');
      case 'acceptError':
        return t('studentAcceptInvite.acceptError');
    }
  }

  const errorMessage = isMissingToken
    ? t('studentAcceptInvite.noToken')
    : serverMessage ?? (errorReason ? reasonMessage(errorReason) : t('studentAcceptInvite.genericError'));

  const inviter = preview
    ? preview.inviteSource === 'Educator'
      ? preview.schoolName ?? t('studentAcceptInvite.fromSchool')
      : t('studentAcceptInvite.fromParent')
    : '';

  return (
    <PageLayout data-testid="student-accept-invite" title={t('studentAcceptInvite.title')}>
      <Card className="max-w-md text-center">
        {status === 'loading' && !isMissingToken && (
          <div className="flex justify-center py-6">
            <Spinner label={t('studentAcceptInvite.loadingInvite')} />
          </div>
        )}

        {(status === 'ready' || status === 'submitting') && preview && (
          <div className="space-y-5">
            <p className="text-sm text-brand-slate-600">
              <Trans
                t={t}
                i18nKey="studentAcceptInvite.invitedAsSentence"
                values={{ inviter, name: preview.linkedToFirstName }}
                // `inviter` and `name` can be user-entered text (a school or
                // inviter's own name) — escape it during interpolation and
                // unescape only for display, so a literal "<" in it can
                // never be parsed as one of the tags below.
                tOptions={{ interpolation: { escapeValue: true } }}
                shouldUnescape
                components={{
                  inviter: <span className="font-medium text-brand-slate-800" />,
                  name: <span className="font-medium text-brand-slate-800" />,
                }}
              />
            </p>

            <label
              className="flex items-start gap-3 text-left text-sm text-brand-slate-700"
              htmlFor="student-consent"
            >
              <input
                id="student-consent"
                type="checkbox"
                checked={consentAccepted}
                onChange={(e) => setConsentAccepted(e.target.checked)}
                className="mt-0.5 h-4 w-4 rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-500"
                data-testid="student-consent-checkbox"
              />
              <span>{t('studentAcceptInvite.consentLabel')}</span>
            </label>

            <Button
              onClick={handleAccept}
              disabled={!consentAccepted}
              loading={status === 'submitting'}
              className="w-full"
              data-testid="student-accept-submit"
            >
              {t('studentAcceptInvite.submit')}
            </Button>
          </div>
        )}

        {(status === 'error' || isMissingToken) && (
          <div className="space-y-4">
            <Notice
              variant="error"
              title={errorMessage}
            />
            <Button variant="secondary" onClick={() => navigate('/dashboard')}>
              {t('studentAcceptInvite.goToDashboard')}
            </Button>
          </div>
        )}
      </Card>
    </PageLayout>
  );
}
