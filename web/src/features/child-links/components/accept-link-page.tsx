import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { acceptLink, previewLink } from '../api/child-links-api';
import type { AcceptedChildLink, ChildLinkInvitePreview } from '../types';
import { ChildLinkChoice, CREATE_NEW } from './child-link-choice';

type Status = 'loading' | 'ready' | 'submitting' | 'success' | 'error';

export function AcceptLinkPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');

  const [status, setStatus] = useState<Status>('loading');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
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
          setErrorMessage(response.message || 'This link is invalid or has expired.');
        }
      } catch {
        if (active) {
          setStatus('error');
          setErrorMessage('An error occurred while loading this link.');
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
    if (!token) return;
    setStatus('submitting');
    setErrorMessage(null);

    const linkToChildProfileId = choice === CREATE_NEW ? undefined : Number(choice);

    try {
      const response = await acceptLink(token, linkToChildProfileId);
      if (response.success && response.data) {
        setAccepted(response.data);
        setStatus('success');
      } else {
        setStatus('error');
        setErrorMessage(response.message || 'Failed to accept this link.');
      }
    } catch {
      setStatus('error');
      setErrorMessage('An error occurred while accepting this link.');
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
        <h1 className="font-serif mb-4">Link to Your School</h1>

        {status === 'loading' && !isMissingToken && (
          <div className="flex justify-center py-6">
            <Spinner label="Loading link…" />
          </div>
        )}

        {(status === 'ready' || status === 'submitting') && preview && (
          <div className="space-y-5">
            <p className="text-sm text-brand-slate-600">
              {preview.schoolName ? (
                <>
                  <span className="font-medium text-brand-slate-800">{preview.schoolName}</span>{' '}
                  invited you to connect{' '}
                </>
              ) : (
                'You were invited to connect '
              )}
              <span className="font-medium text-brand-slate-800">{studentName}</span>.
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
              Accept &amp; Link
            </Button>
          </div>
        )}

        {status === 'success' && (
          <div className="space-y-4">
            <Notice variant="success" title="Linked!">
              {studentName ? `${studentName} is now linked to your account.` : 'The student is now linked to your account.'}
            </Notice>
            <Link to={childHref}>
              <Button data-testid="accept-link-continue">
                {accepted?.childProfileId ? 'View Child' : 'Go to Dashboard'}
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
                  ? 'No link token provided.'
                  : errorMessage || 'Something went wrong'
              }
            />
            <Link to="/dashboard">
              <Button variant="secondary">Go to Dashboard</Button>
            </Link>
          </div>
        )}
      </Card>
    </div>
  );
}
