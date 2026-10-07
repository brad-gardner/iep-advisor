import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Input, Select } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { ORG_ROLE } from '@/features/educator/types';
import type { DistrictSchool } from '@/features/district-admin/types';
import { InviteUrlField } from './invite-url-field';
import type { CreateStaffInviteRequest, StaffInvite } from '../types';

interface InviteFormProps {
  // Caller's org role drives which roles can be invited and whether the school
  // picker is locked.
  callerOrgRoleId: number;
  // For SchoolAdmin callers, the school they belong to (picker is locked to it).
  callerSchoolId?: number | null;
  schools: DistrictSchool[];
  onSubmit: (
    data: CreateStaffInviteRequest
  ) => Promise<{ success: boolean; error?: string; invite?: StaffInvite }>;
}

// School-bound roles either admin tier may invite. These are this form's OWN
// invitable-role labels (what a district admin picks when sending an
// invite), a distinct voice from `orgRoleLabel`'s `common:orgRole.*` (which
// labels an EXISTING member/invite's role elsewhere) — see `inviteForm.roles.*`.
function schoolRoles(t: TFunction<'staff-invites'>): { id: number; label: string }[] {
  return [
    { id: ORG_ROLE.SchoolAdmin, label: t('inviteForm.roles.schoolAdmin') },
    { id: ORG_ROLE.Teacher, label: t('inviteForm.roles.teacher') },
    { id: ORG_ROLE.RelatedServiceProvider, label: t('inviteForm.roles.relatedServiceProvider') },
    { id: ORG_ROLE.GeneralEducator, label: t('inviteForm.roles.generalEducator') },
  ];
}

// Roles a caller may invite. DistrictAdmin can also invite DistrictAdmins;
// SchoolAdmin is limited to the school-bound roles.
function invitableRoles(callerOrgRoleId: number, t: TFunction<'staff-invites'>): { id: number; label: string }[] {
  const roles = schoolRoles(t);
  if (callerOrgRoleId === ORG_ROLE.DistrictAdmin) {
    return [{ id: ORG_ROLE.DistrictAdmin, label: t('inviteForm.roles.districtAdmin') }, ...roles];
  }
  return roles;
}

export function InviteForm({
  callerOrgRoleId,
  callerSchoolId,
  schools,
  onSubmit,
}: InviteFormProps) {
  const { t } = useTranslation('staff-invites');
  const roles = invitableRoles(callerOrgRoleId, t);
  const isCallerDistrictAdmin = callerOrgRoleId === ORG_ROLE.DistrictAdmin;

  const [email, setEmail] = useState('');
  const [orgRoleId, setOrgRoleId] = useState<number>(roles[0].id);
  // SchoolAdmin callers are always locked to their own school; pre-select it.
  const [schoolId, setSchoolId] = useState<string>(
    !isCallerDistrictAdmin && callerSchoolId != null ? String(callerSchoolId) : ''
  );
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [createdUrl, setCreatedUrl] = useState<string | null>(null);
  const [successEmail, setSuccessEmail] = useState<string | null>(null);

  // A DistrictAdmin invitee has no school; the picker is hidden and schoolId is
  // sent as null. Everyone else needs a school.
  const invitingDistrictAdmin = orgRoleId === ORG_ROLE.DistrictAdmin;
  const schoolPickerVisible = !invitingDistrictAdmin;
  // DistrictAdmin callers choose the school; SchoolAdmin callers are locked.
  const schoolPickerLocked = !isCallerDistrictAdmin;
  const needsSchools = schoolPickerVisible && isCallerDistrictAdmin && schools.length === 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setCreatedUrl(null);
    setSuccessEmail(null);

    const trimmedEmail = email.trim();
    if (!trimmedEmail) {
      setError(t('inviteForm.errors.emailRequired'));
      return;
    }

    let resolvedSchoolId: number | undefined;
    if (schoolPickerVisible) {
      const source = schoolPickerLocked ? String(callerSchoolId ?? '') : schoolId;
      if (!source) {
        setError(t('inviteForm.errors.selectSchool'));
        return;
      }
      resolvedSchoolId = Number(source);
    }

    setIsSubmitting(true);
    const result = await onSubmit({
      email: trimmedEmail,
      orgRoleId,
      schoolId: resolvedSchoolId,
    });

    if (result.success) {
      setSuccessEmail(trimmedEmail);
      setCreatedUrl(result.invite?.inviteUrl ?? null);
      setEmail('');
    } else {
      setError(result.error ?? t('inviteForm.errors.couldNotSend'));
    }
    setIsSubmitting(false);
  };

  // DistrictAdmin needs at least one school before inviting school-bound staff.
  if (needsSchools) {
    return (
      <div className="space-y-4" data-testid="district-staff-invite-needs-school">
        <Notice variant="info" title={t('inviteForm.needsSchoolTitle')}>
          {t('inviteForm.needsSchoolBody')}
        </Notice>
        <Link to="/educator/admin/schools">
          <Button data-testid="district-staff-invite-create-school-link">
            {t('inviteForm.goToSchools')}
          </Button>
        </Link>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4" data-testid="district-staff-invite-form">
      {error && <Notice variant="error" title={error} />}
      {successEmail && (
        <Notice variant="success" title={t('inviteForm.inviteSent', { email: successEmail })} />
      )}

      <Input
        id="district-staff-invite-email"
        label={t('inviteForm.emailLabel')}
        type="email"
        required
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        maxLength={256}
        placeholder={t('inviteForm.emailPlaceholder')}
        data-testid="district-staff-invite-email"
      />

      <Select
        id="district-staff-invite-role"
        label={t('inviteForm.roleLabel')}
        value={orgRoleId}
        onChange={(e) => setOrgRoleId(Number(e.target.value))}
        data-testid="district-staff-invite-role"
      >
        {roles.map((role) => (
          <option key={role.id} value={role.id}>
            {role.label}
          </option>
        ))}
      </Select>

      {schoolPickerVisible && (
        <Select
          id="district-staff-invite-school"
          label={t('inviteForm.schoolLabel')}
          value={schoolPickerLocked ? String(callerSchoolId ?? '') : schoolId}
          onChange={(e) => setSchoolId(e.target.value)}
          disabled={schoolPickerLocked}
          data-testid="district-staff-invite-school"
        >
          <option value="">{t('inviteForm.selectSchool')}</option>
          {schools.map((school) => (
            <option key={school.id} value={school.id}>
              {school.name}
            </option>
          ))}
        </Select>
      )}

      <Button
        type="submit"
        disabled={isSubmitting}
        data-testid="district-staff-invite-submit"
      >
        {isSubmitting ? t('inviteForm.sending') : t('inviteForm.submit')}
      </Button>

      {createdUrl && <InviteUrlField url={createdUrl} />}
    </form>
  );
}
