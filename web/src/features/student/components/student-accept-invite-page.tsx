import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { acceptInvite, previewInvite } from '../api/student-invite-api';
import type { StudentInvitePreviewDto } from '../types';

type Status = 'loading' | 'ready' | 'submitting' | 'error';

const CONSENT_LABEL =
  'I understand and consent to activating my student account and participating in my IEP process.';

export function StudentAcceptInvitePage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const navigate = useNavigate();
  const { refreshUser } = useAuth();

  const [status, setStatus] = useState<Status>('loading');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
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
          setErrorMessage(response.message || 'This invite is invalid or has expired.');
        }
      } catch {
        if (active) {
          setStatus('error');
          setErrorMessage('An error occurred while loading this invite.');
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
    setErrorMessage(null);

    try {
      const response = await acceptInvite(token, true);
      if (response.success && response.data) {
        // The accept converts the user to the Student role server-side; refresh
        // the cached user so routing/sidebar reflect the new role before nav.
        await refreshUser();
        navigate('/student', { replace: true });
      } else {
        setStatus('error');
        setErrorMessage(response.message || 'Failed to accept this invite.');
      }
    } catch {
      setStatus('error');
      setErrorMessage('An error occurred while accepting this invite.');
    }
  };

  const inviterContext = preview
    ? preview.inviteSource === 'Educator'
      ? preview.schoolName ?? 'Your school'
      : 'Your parent or guardian'
    : '';

  return (
    <div className="max-w-md mx-auto py-12">
      <Card className="text-center" data-testid="student-accept-invite">
        <h1 className="font-serif mb-4">Activate Your Student Account</h1>

        {status === 'loading' && !isMissingToken && (
          <div className="flex justify-center py-6">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
          </div>
        )}

        {(status === 'ready' || status === 'submitting') && preview && (
          <div className="space-y-5">
            <p className="text-sm text-brand-slate-600">
              <span className="font-medium text-brand-slate-800">{inviterContext}</span>{' '}
              invited you to join your IEP process as{' '}
              <span className="font-medium text-brand-slate-800">
                {preview.linkedToFirstName}
              </span>
              .
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
              <span>{CONSENT_LABEL}</span>
            </label>

            <Button
              onClick={handleAccept}
              disabled={!consentAccepted || status === 'submitting'}
              className="w-full"
              data-testid="student-accept-submit"
            >
              {status === 'submitting' ? 'Activating...' : 'Accept & Activate'}
            </Button>
          </div>
        )}

        {(status === 'error' || isMissingToken) && (
          <div className="space-y-4">
            <Notice
              variant="error"
              title={
                isMissingToken
                  ? 'No invite token provided.'
                  : errorMessage || 'Something went wrong'
              }
            />
            <Button variant="secondary" onClick={() => navigate('/dashboard')}>
              Go to Dashboard
            </Button>
          </div>
        )}
      </Card>
    </div>
  );
}
