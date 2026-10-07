import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Trans, useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { acceptLink, previewLink } from '../api/child-links-api';
import type { AcceptedChildLink, ChildLinkInvitePreview } from '../types';
import { ChildLinkChoice, CREATE_NEW } from './child-link-choice';
import { usePageTitle } from '@/hooks/use-page-title';

type Status = 'loading' | 'ready' | 'submitting' | 'success' | 'error';

// A server-provided message is already resolved text and shown as-is;
// `invalidLink`/`loadError` name a namespace key, translated at RENDER time
// (see `errorMessage` below) rather than load time, so a language switch
// after a failed load shows the new language immediately with no refetch
// (phase 2 review).
type LoadError = { kind: 'server'; message: string } | { kind: 'invalidLink' } | { kind: 'loadError' };

export function AcceptLinkPage() {
  const { t } = useTranslation(['child-links', 'common']);
  usePageTitle(t('acceptLink.pageTitle'));
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');

  const [status, setStatus] = useState<Status>('loading');
  const [loadError, setLoadError] = useState<LoadError | null>(null);
  // Set by `handleAccept`, an event handler (not an effect) — translated
  // immediately with whatever `t` is current at submit time, so it's never
  // subject to the same staleness concern as the load effect's error below.
  const [acceptErrorMessage, setAcceptErrorMessage] = useState<string | null>(null);
  const [preview, setPreview] = useState<ChildLinkInvitePreview | null>(null);
  const [accepted, setAccepted] = useState<AcceptedChildLink | null>(null);
  const [choice, setChoice] = useState<string>(CREATE_NEW);

  useEffect(() => {
    if (!token) return;

    let active = true;
    async function load() {
      try {
        const response = await previewLink(token!);
        if (!active) return;
        if (response.success && response.data) {
          setPreview(response.data);
          setStatus('ready');
        } else {
          setStatus('error');
          setLoadError(response.message ? { kind: 'server', message: response.message } : { kind: 'invalidLink' });
        }
      } catch {
        if (active) {
          setStatus('error');
          setLoadError({ kind: 'loadError' });
        }
      }
    }

    load();
    return () => {
      active = false;
    };
    // `t` deliberately excluded — see the `LoadError` comment above.
  }, [token]);

  const errorMessage = loadError
    ? loadError.kind === 'server'
      ? loadError.message
      : t(`acceptLink.${loadError.kind}`)
    : acceptErrorMessage;

  // Missing token is derived at render time (no setState-in-effect needed).
  const isMissingToken = !token;

  const handleAccept = async () => {
    if (!token) return;
    setStatus('submitting');
    setAcceptErrorMessage(null);

    const linkToChildProfileId = choice === CREATE_NEW ? undefined : Number(choice);

    try {
      const response = await acceptLink(token, linkToChildProfileId);
      if (response.success && response.data) {
        setAccepted(response.data);
        setStatus('success');
      } else {
        setStatus('error');
        setAcceptErrorMessage(response.message || t('acceptLink.acceptFailed'));
      }
    } catch {
      setStatus('error');
      setAcceptErrorMessage(t('acceptLink.acceptError'));
    }
  };

  const studentName = preview
    ? `${preview.studentFirstName} ${preview.studentLastName ?? ''}`.trim()
    : '';

  const childHref = accepted?.childProfileId
    ? `/children/${accepted.childProfileId}`
    : '/dashboard';

  return (
    <div className="max-w-md mx-auto py-12">
      <Card className="text-center">
        <h1 className="font-serif mb-4">{t('acceptLink.heading')}</h1>

        {status === 'loading' && !isMissingToken && (
          <div className="flex justify-center py-6">
            <Spinner label={t('acceptLink.loadingLink')} />
          </div>
        )}

        {(status === 'ready' || status === 'submitting') && preview && (
          <div className="space-y-5">
            <p className="text-sm text-brand-slate-600">
              {preview.schoolName ? (
                <Trans
                  t={t}
                  i18nKey="acceptLink.invitedWithSchool"
                  values={{ school: preview.schoolName, student: studentName }}
                  tOptions={{ interpolation: { escapeValue: true } }}
                  shouldUnescape
                  components={{
                    school: <span className="font-medium text-brand-slate-800" />,
                    student: <span className="font-medium text-brand-slate-800" />,
                  }}
                />
              ) : (
                <Trans
                  t={t}
                  i18nKey="acceptLink.invitedWithoutSchool"
                  values={{ student: studentName }}
                  tOptions={{ interpolation: { escapeValue: true } }}
                  shouldUnescape
                  components={{
                    student: <span className="font-medium text-brand-slate-800" />,
                  }}
                />
              )}
            </p>

            <ChildLinkChoice
              existingChildren={preview.existingChildren}
              value={choice}
              onChange={setChoice}
            />

            <Button
              onClick={handleAccept}
              loading={status === 'submitting'}
              className="w-full"
              data-testid="accept-link-submit"
            >
              {t('acceptLink.acceptAndLink')}
            </Button>
          </div>
        )}

        {status === 'success' && (
          <div className="space-y-4">
            <Notice variant="success" title={t('acceptLink.linkedTitle')}>
              {studentName
                ? t('acceptLink.linkedNamed', { name: studentName })
                : t('acceptLink.linkedGeneric')}
            </Notice>
            <Link to={childHref}>
              <Button data-testid="accept-link-continue">
                {accepted?.childProfileId ? t('acceptLink.viewChild') : t('acceptLink.goToDashboard')}
              </Button>
            </Link>
          </div>
        )}

        {(status === 'error' || isMissingToken) && (
          <div className="space-y-4">
            <Notice
              variant="error"
              title={
                isMissingToken
                  ? t('acceptLink.noToken')
                  : errorMessage || t('common:ui.genericError')
              }
            />
            <Link to="/dashboard">
              <Button variant="secondary">{t('acceptLink.goToDashboard')}</Button>
            </Link>
          </div>
        )}
      </Card>
    </div>
  );
}
