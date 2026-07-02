import { useEffect, useState } from 'react';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Button } from '@/components/ui/button';
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
  const [loadFailed, setLoadFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const response = await getDistrictDashboard();
        if (cancelled) return;
        if (response.success && response.data) setDashboard(response.data);
        else setLoadFailed(true);
      } catch {
        if (!cancelled) setLoadFailed(true);
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // Retry runs from a click handler (not an effect), so it may set state
  // synchronously. Reload the page's dashboard from scratch.
  const retry = async () => {
    setIsLoading(true);
    setLoadFailed(false);
    try {
      const response = await getDistrictDashboard();
      if (response.success && response.data) setDashboard(response.data);
      else setLoadFailed(true);
    } catch {
      setLoadFailed(true);
    } finally {
      setIsLoading(false);
    }
  };

  if (isLoading) {
    return (
      <Card data-testid="district-dashboard-tiles-loading">
        <div className="flex justify-center py-6" role="status">
          <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-brand-teal-500" />
          <span className="sr-only">Loading dashboard…</span>
        </div>
      </Card>
    );
  }

  // A failed load must not silently vanish — the admin should see that the
  // dashboard errored (and be able to retry), not assume the feature is missing.
  if (loadFailed || !dashboard) {
    return (
      <Card data-testid="district-dashboard-tiles-error">
        <Notice variant="error" title="Couldn't load the dashboard">
          <p>Something went wrong loading your district overview.</p>
          <Button
            variant="secondary"
            className="mt-3"
            onClick={retry}
            data-testid="district-dashboard-tiles-retry"
          >
            Try again
          </Button>
        </Notice>
      </Card>
    );
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
