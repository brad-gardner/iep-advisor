import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { getStudentLinks, inviteParent, revokeStudentLink } from '../api/educator-api';
import type { ChildLink } from '../types';
import { InviteParentForm } from './invite-parent-form';
import { StudentLinksList } from './student-links-list';

// "Family" block hosted inside the team panel: the student's parent links,
// the invite entry point and the revoke confirmation. Self-contained so the
// page stays a thin composition.
export function FamilyLinksSection({ studentId }: { studentId: number }) {
  const { t } = useTranslation('educator');
  const { show: showToast } = useToast();
  const [links, setLinks] = useState<ChildLink[]>([]);
  const [isInviteOpen, setIsInviteOpen] = useState(false);
  const [revokeTarget, setRevokeTarget] = useState<ChildLink | null>(null);
  const [revokingId, setRevokingId] = useState<number | null>(null);
  const [revokeNote, setRevokeNote] = useState<string | null>(null);
  const [revokeError, setRevokeError] = useState<string | null>(null);

  const reloadLinks = useCallback(async () => {
    try {
      const response = await getStudentLinks(studentId);
      if (response.success && response.data) setLinks(response.data);
    } catch {
      // Refetch failure keeps the existing links rather than crashing the page.
    }
  }, [studentId]);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await getStudentLinks(studentId);
        if (active && response.success && response.data) setLinks(response.data);
      } catch {
        // Leave the list empty; the empty state communicates "no links".
      }
    })();
    return () => {
      active = false;
    };
  }, [studentId]);

  const handleInvite = async (email: string) => {
    try {
      const response = await inviteParent(studentId, { parentEmail: email });
      if (response.success) {
        await reloadLinks();
        setIsInviteOpen(false);
        showToast({ message: t('familyLinks.inviteSuccess'), variant: 'success' });
        return { success: true, message: response.message };
      }
      return { success: false, message: response.message };
    } catch (err) {
      return { success: false, message: apiErrorMessage(err, t('familyLinks.inviteError')) };
    }
  };

  const confirmRevoke = async () => {
    if (!revokeTarget) return;
    setRevokingId(revokeTarget.id);
    setRevokeNote(null);
    setRevokeError(null);
    try {
      const response = await revokeStudentLink(studentId, revokeTarget.id);
      if (response.success) {
        // Surface the forward-only note from the server (revoke is not retroactive).
        setRevokeNote(response.message || t('familyLinks.linkRevokedDefault'));
        await reloadLinks();
        setRevokeTarget(null);
      } else {
        setRevokeError(response.message || t('familyLinks.revokeError'));
      }
    } catch (err) {
      // Stays in the dialog so the user can retry or cancel.
      setRevokeError(apiErrorMessage(err, t('familyLinks.revokeError')));
    } finally {
      setRevokingId(null);
    }
  };

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <h3 className="text-sm font-medium text-brand-slate-800">{t('familyLinks.heading')}</h3>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => setIsInviteOpen(true)}
          data-testid="invite-parent-open"
        >
          {t('familyLinks.invite')}
        </Button>
      </div>
      {revokeNote && (
        <Notice variant="info" title={t('familyLinks.linkRevokedTitle')}>
          {revokeNote}
        </Notice>
      )}
      <StudentLinksList links={links} revokingId={revokingId} onRevoke={setRevokeTarget} />

      <Modal
        open={isInviteOpen}
        onClose={() => setIsInviteOpen(false)}
        title={t('familyLinks.inviteModalTitle')}
        data-testid="invite-parent-modal"
      >
        <InviteParentForm embedded onInvite={handleInvite} />
      </Modal>

      <ConfirmDialog
        open={revokeTarget !== null}
        title={t('familyLinks.revokeDialogTitle')}
        message={t('familyLinks.revokeDialogMessage')}
        confirmLabel={t('familyLinks.revokeConfirmLabel')}
        loading={revokingId !== null}
        error={revokeError}
        onConfirm={confirmRevoke}
        onCancel={() => {
          setRevokeError(null);
          setRevokeTarget(null);
        }}
        data-testid="student-link-revoke-dialog"
      />
    </div>
  );
}
