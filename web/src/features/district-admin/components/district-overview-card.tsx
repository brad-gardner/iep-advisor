import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { getDistrict } from '../api/district-api';
import type { DistrictOverview } from '../types';

// District-wide summary shown to DistrictAdmins on the educator dashboard.
// Self-contained: fetches its own overview so the dashboard can drop it in for
// admins without threading extra state.
export function DistrictOverviewCard() {
  const [overview, setOverview] = useState<DistrictOverview | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const response = await getDistrict();
        if (!cancelled) {
          setOverview(response.success && response.data ? response.data : null);
        }
      } catch {
        if (!cancelled) setOverview(null);
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
      <Card className="max-w-lg" data-testid="district-overview-card">
        <div className="flex justify-center py-6">
          <Spinner />
        </div>
      </Card>
    );
  }

  if (!overview) {
    return null;
  }

  return (
    <Card className="max-w-lg" accent data-testid="district-overview-card">
      <h2 className="font-serif text-xl mb-4">{overview.name}</h2>
      <dl className="grid grid-cols-2 gap-4 text-sm">
        {overview.stateCode && (
          <div>
            <dt className="text-brand-slate-500">State</dt>
            <dd className="text-brand-slate-800">{overview.stateCode}</dd>
          </div>
        )}
        <div>
          <dt className="text-brand-slate-500">Active schools</dt>
          <dd className="text-brand-slate-800">{overview.activeSchoolCount}</dd>
        </div>
        <div>
          <dt className="text-brand-slate-500">Active staff</dt>
          <dd className="text-brand-slate-800">{overview.activeStaffCount}</dd>
        </div>
      </dl>
      <div className="mt-6">
        <Link to="/educator/admin/schools">
          <Button variant="secondary" data-testid="district-overview-manage-schools">
            Manage schools
          </Button>
        </Link>
      </div>
    </Card>
  );
}
