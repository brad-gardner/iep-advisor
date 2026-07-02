import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { DistrictOverviewCard } from '@/features/district-admin/components/district-overview-card';
import { DistrictDashboardTiles } from '@/features/district-admin/components/district-dashboard-tiles';
import { SetupChecklistCard } from '@/features/district-admin/components/setup-checklist-card';
import { ORG_ROLE } from '../types';
import type { EducatorProfile } from '../types';

interface EducatorDashboardProps {
  profile: EducatorProfile;
}

export function EducatorDashboard({ profile }: EducatorDashboardProps) {
  const isDistrictAdmin = profile.orgRoleId === ORG_ROLE.DistrictAdmin;
  const isAdmin =
    isDistrictAdmin || profile.orgRoleId === ORG_ROLE.SchoolAdmin;

  return (
    <div className="space-y-6">
      {isDistrictAdmin && <SetupChecklistCard />}
      {isDistrictAdmin && <DistrictOverviewCard />}
      {isAdmin && <DistrictDashboardTiles />}

      <Card className="max-w-lg" data-testid="educator-dashboard">
        <h2 className="font-serif text-xl mb-4">
          {profile.schoolName ?? profile.districtName}
        </h2>
        <dl className="space-y-2 text-sm">
          {profile.schoolName && (
            <div className="flex justify-between">
              <dt className="text-brand-slate-500">District</dt>
              <dd className="text-brand-slate-800">{profile.districtName}</dd>
            </div>
          )}
          <div className="flex justify-between">
            <dt className="text-brand-slate-500">Role</dt>
            <dd className="text-brand-slate-800">{profile.orgRoleName}</dd>
          </div>
          {profile.stateCode && (
            <div className="flex justify-between">
              <dt className="text-brand-slate-500">State</dt>
              <dd className="text-brand-slate-800">{profile.stateCode}</dd>
            </div>
          )}
          {profile.title && (
            <div className="flex justify-between">
              <dt className="text-brand-slate-500">Title</dt>
              <dd className="text-brand-slate-800">{profile.title}</dd>
            </div>
          )}
        </dl>
        <div className="mt-6">
          <Link to="/educator/students">
            <Button data-testid="educator-students-link">View Students</Button>
          </Link>
        </div>
      </Card>
    </div>
  );
}
