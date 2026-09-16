import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { CheckCircle2, HelpCircle, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { apiErrorMessage } from '@/lib/api-error';
import { usePageTitle } from '@/hooks/use-page-title';
import { getMeetingByToken, submitTokenRsvp } from '../api/meetings-api';
import { formatMeetingWhen } from '../lib/meeting-time';
import { INVITE_STATUS_LABELS } from '../types';
import type { InviteStatus, TokenRsvpResult } from '../types';

/**
 * Public, unauthenticated page linked from the meeting invitation email
 * (`/meetings/rsvp?token=`). Shows the meeting summary and lets the invitee
 * Accept/Decline/mark Tentative without logging in.
 */
export function MeetingRsvpPage() {
  usePageTitle('Meeting RSVP');
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const [result, setResult] = useState<TokenRsvpResult | null>(null);
  const [error, setError] = useState<string | null>(null);
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
  const missingTokenError = token ? null : 'This link is missing its invitation token.';

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
          setError(response.message ?? 'This invitation link is no longer valid.');
        }
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'This invitation link is no longer valid.'));
      }
    })();
    return () => {
      active = false;
    };
  }, [token, retryToken]);

  const displayError = missingTokenError ?? error;

  const handleRespond = async (status: InviteStatus) => {
    setSubmitting(status);
    setRespondError(null);
    try {
      const response = await submitTokenRsvp({ token, status });
      if (response.success && response.data) {
        setResult(response.data);
        setChanging(false);
      } else {
        setRespondError(response.message ?? 'Could not record your response.');
      }
    } catch (err) {
      setRespondError(apiErrorMessage(err, 'Could not record your response.'));
    } finally {
      setSubmitting(null);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-brand-slate-50 px-4 py-12">
      <Card className="w-full max-w-md" data-testid="meeting-rsvp-card">
        <h1 className="mb-4 font-serif text-lg text-brand-slate-800">Meeting invitation</h1>

        {displayError && (
          <div role="alert">
            <Notice variant="error" title={displayError}>
              {!missingTokenError && (
                <Button size="sm" variant="secondary" onClick={() => setRetryToken((t) => t + 1)}>
                  Try again
                </Button>
              )}
            </Notice>
          </div>
        )}

        {!displayError && !result && (
          <div className="flex justify-center py-8">
            <Spinner label="Loading invitation…" />
          </div>
        )}

        {!displayError && result && (
          <div className="space-y-4">
            <div>
              <p className="font-medium text-brand-slate-800">
                {result.meeting.title || result.meeting.type}
              </p>
              <p className="text-sm text-brand-slate-600">
                {formatMeetingWhen(result.meeting.startsAtUtc, result.meeting.durationMinutes)}
              </p>
              {result.meeting.location && (
                <p className="text-sm text-brand-slate-500">{result.meeting.location}</p>
              )}
            </div>

            {!showPrompt && respondedStatus ? (
              <Notice variant="success" title="Thanks — your response was recorded">
                <p>You responded: {INVITE_STATUS_LABELS[respondedStatus]}.</p>
                <Button
                  variant="ghost"
                  size="sm"
                  className="mt-2"
                  onClick={() => setChanging(true)}
                  data-testid="rsvp-change-response"
                >
                  Change response
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
                  Accept
                </Button>
                <Button
                  variant="secondary"
                  onClick={() => handleRespond('Tentative')}
                  loading={submitting === 'Tentative'}
                  disabled={submitting !== null}
                  data-testid="rsvp-tentative"
                >
                  <HelpCircle className="mr-1.5 h-4 w-4" aria-hidden="true" />
                  Tentative
                </Button>
                <Button
                  variant="danger"
                  onClick={() => handleRespond('Declined')}
                  loading={submitting === 'Declined'}
                  disabled={submitting !== null}
                  data-testid="rsvp-decline"
                >
                  <XCircle className="mr-1.5 h-4 w-4" aria-hidden="true" />
                  Decline
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
