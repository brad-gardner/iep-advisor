import { School } from 'lucide-react';
import { EmptyState } from '@/components/ui/empty-state';
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
      <EmptyState
        data-testid="district-schools-empty"
        icon={School}
        title="No schools yet"
        description="Add your first school using the Add school button to start building out your district."
      />
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
