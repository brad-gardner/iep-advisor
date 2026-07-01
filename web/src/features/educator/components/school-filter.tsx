import { Select } from '@/components/ui/input';
import type { DistrictSchool } from '@/features/district-admin/types';

interface SchoolFilterProps {
  schools: DistrictSchool[];
  // Empty string means "all schools".
  value: string;
  onChange: (value: string) => void;
}

// DistrictAdmin-only client-side filter for the roster, scoping the visible
// students to a single school (or all).
export function SchoolFilter({ schools, value, onChange }: SchoolFilterProps) {
  return (
    <div className="max-w-xs">
      <Select
        id="educator-students-school-filter"
        label="Filter by school"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        data-testid="educator-students-school-filter"
      >
        <option value="">All schools</option>
        {schools.map((school) => (
          <option key={school.id} value={school.id}>
            {school.name}
          </option>
        ))}
      </Select>
    </div>
  );
}
