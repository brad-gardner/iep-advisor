import { useState } from 'react';
import { Select } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import type { StaffMember } from '@/features/staff-invites/types';
import { orgRoleLabel } from '@/lib/org-role-label';
import { ACCESS_ROLES, type AccessRole, type GrantStudentStaffAccessRequest } from '../../types';

interface StaffAccessAssignFormProps {
  // Already filtered to active, school-bound staff of the student's school,
  // minus anyone already granted access.
  eligibleStaff: StaffMember[];
  onAssign: (
    data: GrantStudentStaffAccessRequest
  ) => Promise<{ success: boolean; error?: string }>;
}

export function StaffAccessAssignForm({ eligibleStaff, onAssign }: StaffAccessAssignFormProps) {
  const [staffProfileId, setStaffProfileId] = useState('');
  const [accessRole, setAccessRole] = useState<AccessRole>('Collaborator');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  if (eligibleStaff.length === 0) {
    return (
      <p className="text-sm text-brand-slate-400" data-testid="student-staff-assign-empty">
        All eligible staff at this school already have access.
      </p>
    );
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    if (!staffProfileId) {
      setError('Select a staff member to assign');
      return;
    }

    setIsSubmitting(true);
    const result = await onAssign({
      staffProfileId: Number(staffProfileId),
      accessRole,
    });

    if (result.success) {
      setStaffProfileId('');
      setAccessRole('Collaborator');
      setSuccess('Access granted');
    } else {
      setError(result.error ?? 'Could not assign this staff member');
    }
    setIsSubmitting(false);
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="space-y-4"
      data-testid="student-staff-assign-form"
    >
      {error && <Notice variant="error" title={error} />}
      {success && <Notice variant="success" title={success} />}

      <Select
        id="student-staff-assign-staff"
        label="Staff member *"
        value={staffProfileId}
        onChange={(e) => setStaffProfileId(e.target.value)}
        data-testid="student-staff-assign-staff"
      >
        <option value="">Select a staff member</option>
        {eligibleStaff.map((member) => (
          <option key={member.staffProfileId} value={member.staffProfileId}>
            {`${member.firstName} ${member.lastName}`.trim() || member.email} ·{' '}
            {orgRoleLabel(member.orgRoleName)}
          </option>
        ))}
      </Select>

      <Select
        id="student-staff-assign-role"
        label="Access role"
        value={accessRole}
        onChange={(e) => setAccessRole(e.target.value as AccessRole)}
        data-testid="student-staff-assign-role"
      >
        {ACCESS_ROLES.map((role) => (
          <option key={role} value={role}>
            {role}
          </option>
        ))}
      </Select>

      <Button
        type="submit"
        disabled={isSubmitting}
        data-testid="student-staff-assign-submit"
      >
        {isSubmitting ? 'Assigning...' : 'Assign staff'}
      </Button>
    </form>
  );
}
