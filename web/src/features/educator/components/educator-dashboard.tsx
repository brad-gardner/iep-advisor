import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import type { EducatorProfile } from '../types';

interface EducatorDashboardProps {
  profile: EducatorProfile;
}

export function EducatorDashboard({ profile }: EducatorDashboardProps) {
  return (
    <Card className="max-w-lg" data-testid="educator-dashboard">
      <h2 className="font-serif text-xl mb-4">{profile.schoolName}</h2>
      <dl className="space-y-2 text-sm">
        <div className="flex justify-between">
          <dt className="text-brand-slate-500">District</dt>
          <dd className="text-brand-slate-800">{profile.districtName}</dd>
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
  );
}
