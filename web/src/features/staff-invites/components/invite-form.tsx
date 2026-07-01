import { useState } from 'react';
import { Link } from 'react-router-dom';
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

// Roles a caller may invite. DistrictAdmin can invite all three; SchoolAdmin can
// invite SchoolAdmin/Teacher only.
function invitableRoles(callerOrgRoleId: number): { id: number; label: string }[] {
  if (callerOrgRoleId === ORG_ROLE.DistrictAdmin) {
    return [
      { id: ORG_ROLE.DistrictAdmin, label: 'District Admin' },
      { id: ORG_ROLE.SchoolAdmin, label: 'School Admin' },
      { id: ORG_ROLE.Teacher, label: 'Teacher' },
    ];
  }
  return [
    { id: ORG_ROLE.SchoolAdmin, label: 'School Admin' },
    { id: ORG_ROLE.Teacher, label: 'Teacher' },
  ];
}

export function InviteForm({
  callerOrgRoleId,
  callerSchoolId,
  schools,
  onSubmit,
}: InviteFormProps) {
  const roles = invitableRoles(callerOrgRoleId);
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
      setError('Email is required');
      return;
    }

    let resolvedSchoolId: number | undefined;
    if (schoolPickerVisible) {
      const source = schoolPickerLocked ? String(callerSchoolId ?? '') : schoolId;
      if (!source) {
        setError('Select a school for this invite');
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
      setError(result.error ?? 'Could not send the invite');
    }
    setIsSubmitting(false);
  };

  // DistrictAdmin needs at least one school before inviting school-bound staff.
  if (needsSchools) {
    return (
      <div className="space-y-4" data-testid="district-staff-invite-needs-school">
        <Notice variant="info" title="Add a school first">
          Staff are assigned to a school. Create your first school, then invite
          school admins and teachers.
        </Notice>
        <Link to="/educator/admin/schools">
          <Button data-testid="district-staff-invite-create-school-link">
            Go to Schools
          </Button>
        </Link>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4" data-testid="district-staff-invite-form">
      {error && <Notice variant="error" title={error} />}
      {successEmail && (
        <Notice variant="success" title={`Invite sent to ${successEmail}`} />
      )}

      <Input
        id="district-staff-invite-email"
        label="Work email *"
        type="email"
        required
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        maxLength={256}
        placeholder="name@district.org"
        data-testid="district-staff-invite-email"
      />

      <Select
        id="district-staff-invite-role"
        label="Role *"
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
          label="School *"
          value={schoolPickerLocked ? String(callerSchoolId ?? '') : schoolId}
          onChange={(e) => setSchoolId(e.target.value)}
          disabled={schoolPickerLocked}
          data-testid="district-staff-invite-school"
        >
          <option value="">Select a school</option>
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
        {isSubmitting ? 'Sending...' : 'Send invite'}
      </Button>

      {createdUrl && <InviteUrlField url={createdUrl} />}
    </form>
  );
}
