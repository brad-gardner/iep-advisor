import { useCallback, useEffect, useState } from 'react';
import { Card } from '@/components/ui/card';
import { useEducatorProfile } from '@/features/educator/hooks/use-educator-profile';
import { getDistrictSchools } from '@/features/district-admin/api/district-api';
import type { DistrictSchool } from '@/features/district-admin/types';
import {
  createStaffInvite,
  deactivateStaff,
  getStaffList,
  reactivateStaff,
  resendStaffInvite,
  revokeStaffInvite,
} from '../api/staff-invites-api';
import { InviteForm } from '../components/invite-form';
import { InvitesList } from '../components/invites-list';
import { StaffList } from '../components/staff-list';
import { DeactivateSolelyOwnedNotice } from '../components/deactivate-solely-owned-notice';
import type {
  CreateStaffInviteRequest,
  DeactivateStaffResponse,
  StaffList as StaffListData,
} from '../types';

const EMPTY_LIST: StaffListData = { members: [], pendingInvites: [] };

export function DistrictStaffPage() {
  const { profile } = useEducatorProfile();
  const [staff, setStaff] = useState<StaffListData>(EMPTY_LIST);
  const [schools, setSchools] = useState<DistrictSchool[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  // After a deactivate, surface students that were only accessible to that staff
  // member so an admin can reassign them.
  const [solelyOwned, setSolelyOwned] = useState<DeactivateStaffResponse | null>(null);

  const reloadStaff = useCallback(async () => {
    try {
      const response = await getStaffList();
      setStaff(response.success && response.data ? response.data : EMPTY_LIST);
    } catch {
      setStaff(EMPTY_LIST);
    }
  }, []);

  const reloadSchools = useCallback(async () => {
    try {
      const response = await getDistrictSchools();
      setSchools(response.success && response.data ? response.data : []);
    } catch {
      setSchools([]);
    }
  }, []);

  useEffect(() => {
    let active = true;
    (async () => {
      await Promise.all([reloadStaff(), reloadSchools()]);
      if (active) setIsLoading(false);
    })();
    return () => {
      active = false;
    };
  }, [reloadStaff, reloadSchools]);

  const handleInvite = async (data: CreateStaffInviteRequest) => {
    try {
      const response = await createStaffInvite(data);
      if (response.success && response.data) {
        await reloadStaff();
        return { success: true, invite: response.data };
      }
      return { success: false, error: response.message || 'Failed to send invite' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  const handleRevoke = async (inviteId: number) => {
    try {
      const response = await revokeStaffInvite(inviteId);
      if (response.success) {
        await reloadStaff();
        return { success: true };
      }
      return { success: false, error: response.message || 'Failed to revoke invite' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  const handleResend = async (inviteId: number) => {
    try {
      const response = await resendStaffInvite(inviteId);
      if (response.success) {
        await reloadStaff();
        return { success: true, inviteUrl: response.data?.inviteUrl ?? null };
      }
      return { success: false, error: response.message || 'Failed to resend invite' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  const handleDeactivate = async (staffProfileId: number) => {
    setSolelyOwned(null);
    try {
      const response = await deactivateStaff(staffProfileId);
      if (response.success) {
        await reloadStaff();
        // Surface the reassignment hint at the page level when present.
        if (response.data && response.data.solelyOwnedStudentCount > 0) {
          setSolelyOwned(response.data);
        }
        return { success: true };
      }
      // Backend returns an explicit message for the last-DistrictAdmin guard.
      return {
        success: false,
        error: response.message || 'This staff member cannot be deactivated right now',
      };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  const handleReactivate = async (staffProfileId: number) => {
    try {
      const response = await reactivateStaff(staffProfileId);
      if (response.success) {
        await reloadStaff();
        return { success: true };
      }
      return { success: false, error: response.message || 'Failed to reactivate' };
    } catch {
      return { success: false, error: 'An error occurred' };
    }
  };

  return (
    <div className="space-y-6" data-testid="district-staff-page">
      <h1 className="font-serif">Staff</h1>

      {profile && (
        <Card className="max-w-lg">
          <h2 className="font-serif text-lg mb-4">Invite a staff member</h2>
          <InviteForm
            callerOrgRoleId={profile.orgRoleId}
            callerSchoolId={profile.schoolId}
            schools={schools}
            onSubmit={handleInvite}
          />
        </Card>
      )}

      {isLoading ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
        </div>
      ) : (
        <>
          <section className="space-y-3">
            <h2 className="font-serif text-lg">Pending invites</h2>
            <InvitesList
              invites={staff.pendingInvites}
              onRevoke={handleRevoke}
              onResend={handleResend}
            />
          </section>

          <section className="space-y-3">
            <h2 className="font-serif text-lg">Staff</h2>
            {solelyOwned && (
              <DeactivateSolelyOwnedNotice
                result={solelyOwned}
                onDismiss={() => setSolelyOwned(null)}
              />
            )}
            <StaffList
              members={staff.members}
              onDeactivate={handleDeactivate}
              onReactivate={handleReactivate}
            />
          </section>
        </>
      )}
    </div>
  );
}
