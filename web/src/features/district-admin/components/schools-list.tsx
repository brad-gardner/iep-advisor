import { SchoolRow } from './school-row';
import type { DistrictSchool, SaveSchoolRequest } from '../types';

interface SchoolsListProps {
  schools: DistrictSchool[];
  onUpdate: (
    schoolId: number,
    data: SaveSchoolRequest
  ) => Promise<{ success: boolean; error?: string }>;
  onDeactivate: (schoolId: number) => Promise<{ success: boolean; error?: string }>;
}

export function SchoolsList({ schools, onUpdate, onDeactivate }: SchoolsListProps) {
  if (schools.length === 0) {
    return (
      <p className="text-brand-slate-400 text-sm" data-testid="district-schools-empty">
        No schools yet. Add your first school to get started.
      </p>
    );
  }

  return (
    <ul className="space-y-2" data-testid="district-schools-list">
      {schools.map((school) => (
        <SchoolRow
          key={school.id}
          school={school}
          onUpdate={onUpdate}
          onDeactivate={onDeactivate}
        />
      ))}
    </ul>
  );
}
