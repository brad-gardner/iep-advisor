import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Trans, useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { orgRoleLabel } from '@/lib/org-role-label';
import { usePageTitle } from '@/hooks/use-page-title';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { acceptStaffInvite, previewStaffInvite } from '../api/staff-invites-api';
import { AcceptInviteForm } from '../components/accept-invite-form';
import type { StaffInvitePreview } from '../types';

type Phase = 'loading' | 'ready' | 'error';

export function StaffAcceptInvitePage() {
  const { t } = useTranslation('auth');
  usePageTitle(t('staffAcceptInvite.pageTitle'));
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const navigate = useNavigate();
  const { user, applySession, logout } = useAuth();

  const [phase, setPhase] = useState<Phase>('loading');
  const [preview, setPreview] = useState<StaffInvitePreview | null>(null);
  // The reason (not a pre-localized string) is stored, and resolved to text at
  // render time via `t()` — so a language switch while an error is showing
  // updates the text immediately, without re-running the effect (and
  // re-fetching the preview) just because `t`'s identity changed.
  const [loadFailedReason, setLoadFailedReason] = useState<'server' | 'client' | null>(null);
  const [serverMessage, setServerMessage] = useState<string | null>(null);

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
          setLoadFailedReason('server');
          setServerMessage(response.message || null);
        }
      } catch {
        if (active) {
          setPhase('error');
          setLoadFailedReason('client');
        }
      }
    })();

    return () => {
      active = false;
    };
  }, [token]);

  const loadErrorMessage =
    serverMessage ?? (loadFailedReason === 'client' ? t('staffAcceptInvite.loadError') : t('staffAcceptInvite.loadFailed'));

  const handleAccept = async (data: {
    firstName: string;
    lastName: string;
    password: string;
  }) => {
    if (!token) return { success: false, error: t('staffAcceptInvite.missingTokenError') };
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
        error: response.message || t('staffAcceptInvite.acceptFailed'),
      };
    } catch {
      return { success: false, error: t('staffAcceptInvite.acceptError') };
    }
  };

  // A different user is already signed in. Binding silently would be wrong, so
  // prompt them to sign out and return to this same URL.
  const handleSignOut = () => {
    logout();
  };

  return (
    <div className="w-full text-center">
      <h2 className="text-2xl font-serif font-semibold mb-6 text-brand-slate-800">{t('staffAcceptInvite.heading')}</h2>

      {phase === 'loading' && !isMissingToken && (
        <div className="flex justify-center py-6">
          <Spinner label={t('staffAcceptInvite.loadingInvite')} />
        </div>
      )}

      {(phase === 'error' || isMissingToken) && (
        <Notice
          variant="error"
          title={isMissingToken ? t('staffAcceptInvite.missingTokenTitle') : loadErrorMessage}
        />
      )}

      {phase === 'ready' && preview && preview.status === 'expired' && (
        <Notice variant="warning" title={t('staffAcceptInvite.expiredTitle')}>
          {t('staffAcceptInvite.expiredDetail')}
        </Notice>
      )}

      {phase === 'ready' && preview && preview.status === 'invalid' && (
        <Notice
          variant="error"
          title={t('staffAcceptInvite.invalidTitle')}
        />
      )}

      {phase === 'ready' && preview && preview.status === 'valid' && (
        <div className="space-y-5">
          <p className="text-sm text-brand-slate-600">
            <Trans
              t={t}
              i18nKey="staffAcceptInvite.invitedAsSentence"
              values={{
                district: preview.districtName,
                schoolSuffix: preview.schoolName ? ` · ${preview.schoolName}` : '',
                role: orgRoleLabel(preview.roleName),
              }}
              // `district`/`schoolSuffix` come from the district's own
              // naming — escape it during interpolation and unescape only
              // for display, so a literal "<" in it can never be parsed as
              // one of the tags below.
              tOptions={{ interpolation: { escapeValue: true } }}
              shouldUnescape
              components={{
                district: <span className="font-medium text-brand-slate-800" />,
                role: <span className="font-medium text-brand-slate-800" />,
              }}
            />
          </p>

          {user ? (
            <div className="space-y-4" data-testid="staff-accept-signed-in">
              <Notice variant="info" title={t('staffAcceptInvite.sentTo', { email: preview.email })}>
                {t('staffAcceptInvite.signedInAs', { email: user.email })}
              </Notice>
              <Button
                onClick={handleSignOut}
                className="w-full"
                data-testid="staff-accept-signout"
              >
                {t('staffAcceptInvite.signOut')}
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
