import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import type { DashboardStaffSummary } from '../types';

interface DashboardStaffTileProps {
  staffSummary: DashboardStaffSummary;
}

// Staff status summary (active / deactivated / invited counts). Presentational:
// the composing container owns the single dashboard fetch.
export function DashboardStaffTile({ staffSummary }: DashboardStaffTileProps) {
  return (
    <Card data-testid="dashboard-staff-tile">
      <h2 className="font-serif text-xl mb-4">Staff</h2>

      <dl className="grid grid-cols-3 gap-4 text-sm">
        <div>
          <dt className="text-brand-slate-500">Active</dt>
          <dd
            className="text-brand-slate-800 text-lg font-medium"
            data-testid="dashboard-staff-active-count"
          >
            {staffSummary.activeCount}
          </dd>
        </div>
        <div>
          <dt className="text-brand-slate-500">Invited</dt>
          <dd
            className="text-brand-slate-800 text-lg font-medium"
            data-testid="dashboard-staff-invited-count"
          >
            {staffSummary.invitedCount}
          </dd>
        </div>
        <div>
          <dt className="text-brand-slate-500">Deactivated</dt>
          <dd
            className="text-brand-slate-800 text-lg font-medium"
            data-testid="dashboard-staff-deactivated-count"
          >
            {staffSummary.deactivatedCount}
          </dd>
        </div>
      </dl>

      <div className="mt-4">
        <Link
          to="/educator/admin/staff"
          className="text-sm text-brand-teal-600 hover:underline"
          data-testid="dashboard-staff-tile-link"
        >
          Manage staff
        </Link>
      </div>
    </Card>
  );
}
