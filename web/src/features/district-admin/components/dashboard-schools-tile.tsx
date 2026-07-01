import { Link } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import type { DashboardSchool } from '../types';

interface DashboardSchoolsTileProps {
  schools: DashboardSchool[];
}

// Per-school active student counts. Presentational: the composing container
// (DistrictDashboardTiles) owns the single dashboard fetch.
export function DashboardSchoolsTile({ schools }: DashboardSchoolsTileProps) {
  return (
    <Card data-testid="dashboard-schools-tile">
      <h2 className="font-serif text-xl mb-4">Schools</h2>

      {schools.length === 0 ? (
        <p
          className="text-sm text-brand-slate-400"
          data-testid="dashboard-schools-tile-empty"
        >
          No schools yet — school-by-school student counts will appear here.
        </p>
      ) : (
        <ul className="space-y-2 text-sm">
          {schools.map((school) => (
            <li
              key={school.id}
              className="flex justify-between gap-3"
              data-testid={`dashboard-school-${school.id}`}
            >
              <span className="text-brand-slate-800">{school.name}</span>
              <span className="text-brand-slate-500">
                {school.activeStudentCount}{' '}
                {school.activeStudentCount === 1 ? 'student' : 'students'}
              </span>
            </li>
          ))}
        </ul>
      )}

      <div className="mt-4">
        <Link
          to="/educator/admin/schools"
          className="text-sm text-brand-teal-600 hover:underline"
          data-testid="dashboard-schools-tile-link"
        >
          Manage schools
        </Link>
      </div>
    </Card>
  );
}
