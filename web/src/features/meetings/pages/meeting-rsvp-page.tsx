import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { CheckCircle2, HelpCircle, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { apiErrorMessage } from '@/lib/api-error';
import { getMeetingByToken, submitTokenRsvp } from '../api/meetings-api';
import { formatMeetingWhen } from '../lib/meeting-time';
import type { InviteStatus, TokenRsvpResult } from '../types';

/**
 * Public, unauthenticated page linked from the meeting invitation email
 * (`/meetings/rsvp?token=`). Shows the meeting summary and lets the invitee
 * Accept/Decline/mark Tentative without logging in.
 */
export function MeetingRsvpPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const [result, setResult] = useState<TokenRsvpResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState<InviteStatus | null>(null);
  const [confirmedStatus, setConfirmedStatus] = useState<InviteStatus | null>(null);

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
        if (response.success && response.data) setResult(response.data);
        else setError(response.message ?? 'This invitation link is no longer valid.');
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'This invitation link is no longer valid.'));
      }
    })();
    return () => {
      active = false;
    };
  }, [token]);

  const displayError = missingTokenError ?? error;

  const handleRespond = async (status: InviteStatus) => {
    setSubmitting(status);
    setError(null);
    try {
      const response = await submitTokenRsvp({ token, status });
      if (response.success && response.data) {
        setResult(response.data);
        setConfirmedStatus(status);
      } else {
        setError(response.message ?? 'Could not record your response.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not record your response.'));
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
            <Notice variant="error" title={displayError} />
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

            {confirmedStatus ? (
              <Notice variant="success" title="Thanks — your response was recorded">
                You responded: {confirmedStatus}.
              </Notice>
            ) : (
              <div className="flex flex-wrap gap-2">
                <Button
                  onClick={() => handleRespond('Accepted')}
                  loading={submitting === 'Accepted'}
                  data-testid="rsvp-accept"
                >
                  <CheckCircle2 className="mr-1.5 h-4 w-4" aria-hidden="true" />
                  Accept
                </Button>
                <Button
                  variant="secondary"
                  onClick={() => handleRespond('Tentative')}
                  loading={submitting === 'Tentative'}
                  data-testid="rsvp-tentative"
                >
                  <HelpCircle className="mr-1.5 h-4 w-4" aria-hidden="true" />
                  Tentative
                </Button>
                <Button
                  variant="danger"
                  onClick={() => handleRespond('Declined')}
                  loading={submitting === 'Declined'}
                  data-testid="rsvp-decline"
                >
                  <XCircle className="mr-1.5 h-4 w-4" aria-hidden="true" />
                  Decline
                </Button>
              </div>
            )}
          </div>
        )}
      </Card>
    </div>
  );
}
