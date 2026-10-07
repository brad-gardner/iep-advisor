import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Modal } from '@/components/ui/modal';
import { Notice } from '@/components/ui/notice';
import { Select } from '@/components/ui/input';
import { getStaffList } from '@/features/staff-invites/api/staff-invites-api';
import type { StaffMember } from '@/features/staff-invites/types';
import { orgRoleLabel } from '@/lib/org-role-label';
import { ORG_ROLE } from '../../types';

interface AssignCaseManagerModalProps {
  open: boolean;
  studentCount: number;
  onClose: () => void;
  onAssign: (userId: number) => Promise<{ success: boolean; error?: string }>;
}

// Staff eligible to be a lead case manager: active, and not a DistrictAdmin
// (admins act by scope and are never team members). School/provider
// eligibility per student is enforced server-side.
function eligibleStaff(members: StaffMember[]): StaffMember[] {
  return members.filter((m) => m.isActive && m.orgRoleId !== ORG_ROLE.DistrictAdmin);
}

export function AssignCaseManagerModal({
  open,
  studentCount,
  onClose,
  onAssign,
}: AssignCaseManagerModalProps) {
  const { t } = useTranslation('educator');
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={t('assignCaseManager.title')}
      data-testid="roster-assign-case-manager-modal"
    >
      {/* Remounts per open so the picker/errors reset with the dialog. */}
      <AssignCaseManagerForm studentCount={studentCount} onAssign={onAssign} />
    </Modal>
  );
}

function AssignCaseManagerForm({
  studentCount,
  onAssign,
}: Pick<AssignCaseManagerModalProps, 'studentCount' | 'onAssign'>) {
  const { t } = useTranslation('educator');
  const [staff, setStaff] = useState<StaffMember[] | null>(null);
  const [userId, setUserId] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await getStaffList();
        if (active) setStaff(response.success && response.data ? eligibleStaff(response.data.members) : []);
      } catch {
        if (active) setStaff([]);
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!userId) {
      setError(t('assignCaseManager.selectStaffRequired'));
      return;
    }
    setIsSubmitting(true);
    const result = await onAssign(Number(userId));
    if (!result.success) setError(result.error ?? t('studentsPage.assignCaseManagerFailed'));
    setIsSubmitting(false);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4" data-testid="roster-assign-case-manager-form">
      <p className="text-sm text-brand-slate-600">
        {t('assignCaseManager.description', { count: studentCount })}
      </p>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <Select
        id="roster-assign-case-manager-staff"
        label={t('assignCaseManager.caseManagerLabel')}
        value={userId}
        onChange={(e) => setUserId(e.target.value)}
        disabled={staff === null}
        data-testid="roster-assign-case-manager-staff"
      >
        <option value="">
          {staff === null ? t('assignCaseManager.loadingStaff') : t('assignCaseManager.selectStaff')}
        </option>
        {staff?.map((member) => (
          <option key={member.staffProfileId} value={member.userId}>
            {`${member.firstName} ${member.lastName}`.trim() || member.email} ·{' '}
            {orgRoleLabel(member.orgRoleName)}
            {member.schoolName ? ` · ${member.schoolName}` : ''}
          </option>
        ))}
      </Select>

      <Button
        type="submit"
        loading={isSubmitting}
        className="w-full"
        data-testid="roster-assign-case-manager-submit"
      >
        {t('assignCaseManager.submit')}
      </Button>
    </form>
  );
}
