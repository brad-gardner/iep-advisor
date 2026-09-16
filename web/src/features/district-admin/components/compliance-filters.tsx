import { Button } from '@/components/ui/button';
import { SchoolFilter } from '@/features/educator/components/school-filter';
import { COMPLIANCE_RANGE_PRESETS, type ComplianceRangeDays } from '../lib/date-range';
import type { DistrictSchool } from '../types';

interface ComplianceFiltersProps {
  /** Omit entirely for SchoolAdmin — their school is forced server-side. */
  schools?: DistrictSchool[];
  schoolId: number | null;
  onSchoolChange: (schoolId: number | null) => void;
  rangeDays: ComplianceRangeDays;
  onRangeChange: (days: ComplianceRangeDays) => void;
}

/** School picker (DistrictAdmin only) + due-date-range presets for the
 * compliance board. Overdue counts always ignore the range. */
export function ComplianceFilters({
  schools,
  schoolId,
  onSchoolChange,
  rangeDays,
  onRangeChange,
}: ComplianceFiltersProps) {
  return (
    <div className="flex flex-wrap items-end gap-4" data-testid="compliance-filters">
      {schools && (
        <div className="w-full max-w-xs">
          <SchoolFilter
            schools={schools}
            value={schoolId != null ? String(schoolId) : ''}
            onChange={(value) => onSchoolChange(value ? Number(value) : null)}
          />
        </div>
      )}

      <div role="group" aria-label="Due within" className="flex gap-2">
        {COMPLIANCE_RANGE_PRESETS.map((days) => (
          <Button
            key={days}
            type="button"
            size="sm"
            variant={rangeDays === days ? 'primary' : 'secondary'}
            aria-pressed={rangeDays === days}
            onClick={() => onRangeChange(days)}
            data-testid={`compliance-filter-range-${days}`}
          >
            {days} days
          </Button>
        ))}
      </div>
    </div>
  );
}
