import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { orgRoleLabel } from '@/lib/org-role-label';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { acceptStaffInvite, previewStaffInvite } from '../api/staff-invites-api';
import { AcceptInviteForm } from '../components/accept-invite-form';
import type { StaffInvitePreview } from '../types';

type Phase = 'loading' | 'ready' | 'error';

export function StaffAcceptInvitePage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const navigate = useNavigate();
  const { user, applySession, logout } = useAuth();

  const [phase, setPhase] = useState<Phase>('loading');
  const [preview, setPreview] = useState<StaffInvitePreview | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Missing token is derived at render time (no setState-in-effect needed).
  const isMissingToken = !token;

  useEffect(() => {
    if (!token) return;

    let active = true;
    (async () => {
      try {
        const response = await previewStaffInvite(token);
        if (!active) return;
        if (response.success && response.data) {
          setPreview(response.data);
          setPhase('ready');
        } else {
          setPhase('error');
          setErrorMessage(response.message || 'This invite is invalid or has been claimed.');
        }
      } catch {
        if (active) {
          setPhase('error');
          setErrorMessage('An error occurred while loading this invite.');
        }
      }
    })();

    return () => {
      active = false;
    };
  }, [token]);

  const handleAccept = async (data: {
    firstName: string;
    lastName: string;
    password: string;
  }) => {
    if (!token) return { success: false, error: 'Missing invite token' };
    try {
      const response = await acceptStaffInvite({ token, ...data });
      if (response.success && response.data?.token && response.data.user) {
        // Persist through the single source of truth, same as login/registerDistrict.
        applySession(response.data.token, response.data.user);
        navigate('/educator');
        return { success: true };
      }
      return {
        success: false,
        error: response.message || 'Could not accept this invite.',
      };
    } catch {
      return { success: false, error: 'An error occurred while accepting this invite.' };
    }
  };

  // A different user is already signed in. Binding silently would be wrong, so
  // prompt them to sign out and return to this same URL.
  const handleSignOut = () => {
    logout();
  };

  const heading = 'Join your school team';

  return (
    <div className="w-full text-center">
      <h2 className="text-2xl font-serif font-semibold mb-6 text-brand-slate-800">{heading}</h2>

      {phase === 'loading' && !isMissingToken && (
        <div className="flex justify-center py-6">
          <Spinner label="Loading invite…" />
        </div>
      )}

      {(phase === 'error' || isMissingToken) && (
        <Notice
          variant="error"
          title={
            isMissingToken
              ? 'This invite link is missing its token.'
              : errorMessage || 'This invite is invalid or has been claimed.'
          }
        />
      )}

      {phase === 'ready' && preview && preview.status === 'expired' && (
        <Notice variant="warning" title="This invite has expired">
          Ask your administrator to resend the invite.
        </Notice>
      )}

      {phase === 'ready' && preview && preview.status === 'invalid' && (
        <Notice
          variant="error"
          title="This invite is invalid or has already been claimed."
        />
      )}

      {phase === 'ready' && preview && preview.status === 'valid' && (
        <div className="space-y-5">
          <p className="text-sm text-brand-slate-600">
            <span className="font-medium text-brand-slate-800">{preview.districtName}</span>
            {preview.schoolName ? ` · ${preview.schoolName}` : ''} invited you to join as{' '}
            <span className="font-medium text-brand-slate-800">{orgRoleLabel(preview.roleName)}</span>.
          </p>

          {user ? (
            <div className="space-y-4" data-testid="staff-accept-signed-in">
              <Notice variant="info" title={`This invite was sent to ${preview.email}.`}>
                You are signed in as {user.email}. Sign out to continue.
              </Notice>
              <Button
                onClick={handleSignOut}
                className="w-full"
                data-testid="staff-accept-signout"
              >
                Sign out
              </Button>
            </div>
          ) : (
            <AcceptInviteForm email={preview.email ?? ''} onSubmit={handleAccept} />
          )}
        </div>
      )}
    </div>
  );
}
