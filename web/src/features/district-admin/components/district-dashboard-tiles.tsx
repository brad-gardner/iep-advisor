import { useEffect, useState } from 'react';
import { Card } from '@/components/ui/card';
import { getDistrictDashboard } from '../api/district-api';
import type { DistrictDashboard } from '../types';
import { DashboardSchoolsTile } from './dashboard-schools-tile';
import { DashboardStaffTile } from './dashboard-staff-tile';
import { DashboardInvitesTile } from './dashboard-invites-tile';
import { DashboardAttentionTile } from './dashboard-attention-tile';

// Owns the single oversight-dashboard fetch and fans the result out to the
// presentational tiles, so admins get one loading state instead of four. Shown
// to both admin tiers; SchoolAdmin receives a server-sliced own-school view.
export function DistrictDashboardTiles() {
  const [dashboard, setDashboard] = useState<DistrictDashboard | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const response = await getDistrictDashboard();
        if (!cancelled) {
          setDashboard(response.success && response.data ? response.data : null);
        }
      } catch {
        if (!cancelled) setDashboard(null);
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  if (isLoading) {
    return (
      <Card data-testid="district-dashboard-tiles-loading">
        <div className="flex justify-center py-6">
          <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-brand-teal-500" />
        </div>
      </Card>
    );
  }

  if (!dashboard) {
    return null;
  }

  const hasStudents = dashboard.schools.some((s) => s.activeStudentCount > 0);

  return (
    <div className="grid gap-6 md:grid-cols-2" data-testid="district-dashboard-tiles">
      <DashboardSchoolsTile schools={dashboard.schools} />
      <DashboardStaffTile staffSummary={dashboard.staffSummary} />
      <DashboardInvitesTile invites={dashboard.invitesNeedingAttention} />
      <DashboardAttentionTile
        studentsWithoutStaff={dashboard.studentsWithoutStaff}
        studentsWithoutParent={dashboard.studentsWithoutParent}
        hasStudents={hasStudents}
      />
    </div>
  );
}
