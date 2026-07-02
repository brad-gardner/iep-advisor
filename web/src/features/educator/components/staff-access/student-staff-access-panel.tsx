import { useCallback, useEffect, useMemo, useState } from 'react';
import { Card } from '@/components/ui/card';
import { Spinner } from '@/components/ui/spinner';
import { useToast } from '@/components/ui/toast';
import {
  getStudentStaffAccess,
  grantStudentStaffAccess,
  revokeStudentStaffAccess,
} from '../../api/educator-api';
import { getStaffList } from '@/features/staff-invites/api/staff-invites-api';
import type { StaffMember } from '@/features/staff-invites/types';
import type { GrantStudentStaffAccessRequest, StudentStaffAccess } from '../../types';
import { StaffAccessRow } from './staff-access-row';
import { StaffAccessAssignForm } from './staff-access-assign-form';

interface StudentStaffAccessPanelProps {
  studentId: number;
  // The student's school — eligible staff are filtered to active, school-bound
  // members of this school.
  studentSchoolId: number;
  // Only DistrictAdmin/SchoolAdmin callers can assign/revoke; teachers see the
  // list read-only.
  canManage: boolean;
}

export function StudentStaffAccessPanel({
  studentId,
  studentSchoolId,
  canManage,
}: StudentStaffAccessPanelProps) {
  const { show: showToast } = useToast();
  const [grants, setGrants] = useState<StudentStaffAccess[]>([]);
  const [staff, setStaff] = useState<StaffMember[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const reloadGrants = useCallback(async () => {
    try {
      const response = await getStudentStaffAccess(studentId);
      setGrants(response.success && response.data ? response.data : []);
    } catch {
      setGrants([]);
    }
  }, [studentId]);

  useEffect(() => {
    let active = true;
    (async () => {
      // Teachers cannot assign, so they only need the grant list. Admins also
      // load the staff directory to populate the assign picker.
      const tasks: Promise<unknown>[] = [reloadGrants()];
      if (canManage) {
        tasks.push(
          getStaffList()
            .then((res) => {
              if (active) setStaff(res.success && res.data ? res.data.members : []);
            })
            .catch(() => {
              if (active) setStaff([]);
            })
        );
      }
      await Promise.all(tasks);
      if (active) setIsLoading(false);
    })();
    return () => {
      active = false;
    };
  }, [reloadGrants, canManage]);

  const handleAssign = async (data: GrantStudentStaffAccessRequest) => {
    try {
      const response = await grantStudentStaffAccess(studentId, data);
      if (response.success) {
        await reloadGrants();
        showToast({ message: 'Access granted', variant: 'success' });
        return { success: true };
      }
      return { success: false, error: response.message || 'Could not assign staff' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  const handleRevoke = async (accessId: number) => {
    try {
      const response = await revokeStudentStaffAccess(studentId, accessId);
      if (response.success) {
        await reloadGrants();
        showToast({ message: 'Access revoked', variant: 'success' });
        return { success: true };
      }
      return { success: false, error: response.message || 'Could not revoke access' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  // Eligible = active, bound to this student's school, not already granted.
  const eligibleStaff = useMemo(() => {
    const grantedProfileIds = new Set(grants.map((g) => g.staffProfileId));
    return staff.filter(
      (m) =>
        m.isActive &&
        m.schoolId === studentSchoolId &&
        !grantedProfileIds.has(m.staffProfileId)
    );
  }, [staff, grants, studentSchoolId]);

  return (
    <Card className="max-w-lg" data-testid="student-staff-access-panel">
      {isLoading ? (
        <div className="flex justify-center py-6">
          <Spinner label="Loading assigned staff…" />
        </div>
      ) : (
        <div className="space-y-4">
          {grants.length === 0 ? (
            <p
              className="text-sm text-brand-slate-400"
              data-testid="student-staff-access-empty"
            >
              No staff assigned yet.
            </p>
          ) : (
            <ul className="space-y-2">
              {grants.map((grant) => (
                <StaffAccessRow
                  key={grant.accessId}
                  grant={grant}
                  canManage={canManage}
                  onRevoke={handleRevoke}
                />
              ))}
            </ul>
          )}

          {canManage && (
            <div className="border-t border-brand-slate-100 pt-4">
              <StaffAccessAssignForm eligibleStaff={eligibleStaff} onAssign={handleAssign} />
            </div>
          )}
        </div>
      )}
    </Card>
  );
}
