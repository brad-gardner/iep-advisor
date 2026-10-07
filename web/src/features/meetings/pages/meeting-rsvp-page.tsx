import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { CheckCircle2, HelpCircle, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { apiErrorMessage } from '@/lib/api-error';
import { inviteStatusLabel } from '@/lib/invite-status-label';
import { meetingTypeLabel } from '@/lib/meeting-labels';
import { usePageTitle } from '@/hooks/use-page-title';
import { useLanguageQueryParam } from '@/lib/i18n/use-language-query-param';
import { getMeetingByToken, submitTokenRsvp } from '../api/meetings-api';
import { formatMeetingWhen } from '../lib/meeting-time';
import type { InviteStatus, TokenRsvpResult } from '../types';

// A server-provided message is already resolved text and is shown as-is;
// the generic fallback is translated at RENDER time (see `displayError`
// below), not stored pre-translated here, so the mount effect never needs
// `t` in its dependency array (same idiom as `useHome`).
type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

/**
 * Public, unauthenticated page linked from the meeting invitation email
 * (`/meetings/rsvp?token=`). Shows the meeting summary and lets the invitee
 * Accept/Decline/mark Tentative without logging in.
 */
export function MeetingRsvpPage() {
  const { t } = useTranslation(['meetings', 'common']);
  usePageTitle(t('rsvpPage.pageTitle'));
  // This page owns its own chrome (no AuthLayout), so it honors `?lang=`
  // itself rather than inheriting the layout's handling — same as
  // `CancelDeletionPage` (also a public, token-linked page outside any
  // layout). The plan's Phase 4 email work sends this link with `?lang=`.
  useLanguageQueryParam();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const [result, setResult] = useState<TokenRsvpResult | null>(null);
  const [error, setError] = useState<LoadError | null>(null);
  const [submitting, setSubmitting] = useState<InviteStatus | null>(null);
  // A failed submit is shown inline next to the buttons; it never replaces the
  // invitation (the load error above does that, with its own retry).
  const [respondError, setRespondError] = useState<string | null>(null);
  // Whether the invitee is actively (re-)choosing a response, overriding the
  // "already responded" view derived below. Reset on every fresh token load
  // (see `seenToken`) and after a successful submit, so a revisit — the whole
  // point of an emailed link — always starts on the recorded-answer view
  // rather than defaulting back to the prompt.
  const [changing, setChanging] = useState(false);
  const [seenToken, setSeenToken] = useState<string | null>(null);
  if (token !== seenToken) {
    setSeenToken(token);
    setChanging(false);
    setRespondError(null);
  }
  // Bumped by the "Try again" button to re-run the token load effect below.
  const [retryToken, setRetryToken] = useState(0);

  // The server's own record of this invitee's response — not a "did I submit
  // in this page session" flag — so reopening the same link later shows the
  // true recorded answer instead of a fresh, unanswered-looking prompt.
  const respondedStatus = result && result.status !== 'Pending' ? result.status : null;
  const showPrompt = !respondedStatus || changing;

  // A missing token is a pure function of the URL — derived directly rather
  // than written into `error` state from an effect.
  const missingTokenError = token ? null : t('rsvpPage.missingToken');

  useEffect(() => {
    if (!token) return;
    let active = true;
    (async () => {
      try {
        const response = await getMeetingByToken(token);
        if (!active) return;
        if (response.success && response.data) {
          setResult(response.data);
          setError(null);
        } else {
          setError(response.message ? { kind: 'server', message: response.message } : { kind: 'generic' });
        }
      } catch (err) {
        if (!active) return;
        const serverMessage = apiErrorMessage(err, '');
        setError(serverMessage ? { kind: 'server', message: serverMessage } : { kind: 'generic' });
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded (see `use-home.ts`): re-running this fetch on
    // a plain language switch would be wasteful. The generic fallback is
    // translated below, at render, from `error`'s stored KIND rather than a
    // snapshot string, so it already follows the active language.
  }, [token, retryToken]);

  const displayError =
    missingTokenError ?? (error ? (error.kind === 'server' ? error.message : t('rsvpPage.invalidLink')) : null);

  // Click-triggered (never a mount effect), so translating inline here is
  // safe — see `AcknowledgeControl` (shared-drafts) for the same reasoning.
  const handleRespond = async (status: InviteStatus) => {
    setSubmitting(status);
    setRespondError(null);
    try {
      const response = await submitTokenRsvp({ token, status });
      if (response.success && response.data) {
        setResult(response.data);
        setChanging(false);
      } else {
        setRespondError(response.message || t('rsvpPage.submitFailed'));
      }
    } catch (err) {
      setRespondError(apiErrorMessage(err, t('rsvpPage.submitFailed')));
    } finally {
      setSubmitting(null);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-brand-slate-50 px-4 py-12">
      <Card className="w-full max-w-md" data-testid="meeting-rsvp-card">
        <h1 className="mb-4 font-serif text-lg text-brand-slate-800">{t('rsvpPage.heading')}</h1>

        {displayError && (
          <div role="alert">
            <Notice variant="error" title={displayError}>
              {!missingTokenError && (
                <Button size="sm" variant="secondary" onClick={() => setRetryToken((n) => n + 1)}>
                  {t('common:ui.tryAgain')}
                </Button>
              )}
            </Notice>
          </div>
        )}

        {!displayError && !result && (
          <div className="flex justify-center py-8">
            <Spinner label={t('rsvpPage.loadingInvitation')} />
          </div>
        )}

        {!displayError && result && (
          <div className="space-y-4">
            <div>
              <p className="font-medium text-brand-slate-800">
                {result.meeting.title || meetingTypeLabel(result.meeting.type)}
              </p>
              <p className="text-sm text-brand-slate-600">
                {formatMeetingWhen(result.meeting.startsAtUtc, result.meeting.durationMinutes)}
              </p>
              {result.meeting.location && (
                <p className="text-sm text-brand-slate-500">{result.meeting.location}</p>
              )}
            </div>

            {!showPrompt && respondedStatus ? (
              <Notice variant="success" title={t('rsvpPage.respondedTitle')}>
                <p>{t('rsvpPage.respondedBody', { status: inviteStatusLabel(respondedStatus) })}</p>
                <Button
                  variant="ghost"
                  size="sm"
                  className="mt-2"
                  onClick={() => setChanging(true)}
                  data-testid="rsvp-change-response"
                >
                  {t('rsvpPage.changeResponse')}
                </Button>
              </Notice>
            ) : (
              <div className="space-y-3">
                {respondError && (
                  <div role="alert">
                    <Notice variant="error" title={respondError} />
                  </div>
                )}
              <div className="flex flex-wrap gap-2">
                <Button
                  onClick={() => handleRespond('Accepted')}
                  loading={submitting === 'Accepted'}
                  disabled={submitting !== null}
                  data-testid="rsvp-accept"
                >
                  <CheckCircle2 className="mr-1.5 h-4 w-4" aria-hidden="true" />
                  {t('common:inviteStatus.action.accept')}
                </Button>
                <Button
                  variant="secondary"
                  onClick={() => handleRespond('Tentative')}
                  loading={submitting === 'Tentative'}
                  disabled={submitting !== null}
                  data-testid="rsvp-tentative"
                >
                  <HelpCircle className="mr-1.5 h-4 w-4" aria-hidden="true" />
                  {t('common:inviteStatus.action.tentative')}
                </Button>
                <Button
                  variant="danger"
                  onClick={() => handleRespond('Declined')}
                  loading={submitting === 'Declined'}
                  disabled={submitting !== null}
                  data-testid="rsvp-decline"
                >
                  <XCircle className="mr-1.5 h-4 w-4" aria-hidden="true" />
                  {t('common:inviteStatus.action.decline')}
                </Button>
              </div>
              </div>
            )}
          </div>
        )}
      </Card>
    </div>
  );
}
