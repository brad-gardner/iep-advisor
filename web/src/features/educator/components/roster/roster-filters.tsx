import { useEffect, useRef, useState } from 'react';
import { Input, Select } from '@/components/ui/input';
import type { DistrictSchool } from '@/features/district-admin/types';
import {
  GRADE_LEVELS,
  GRADE_LEVEL_LABELS,
  STUDENT_STATUSES,
  STUDENT_STATUS_LABELS,
} from '../../types';
import type { GradeLevel, StudentStatusFilter } from '../../types';
import type { RosterQuery, RosterQueryPatch } from '../../hooks/use-roster-query';
import { SchoolFilter } from '../school-filter';

export const SEARCH_DEBOUNCE_MS = 300;

interface RosterFiltersProps {
  value: RosterQuery;
  onChange: (patch: RosterQueryPatch) => void;
  // DistrictAdmin only: the district's schools for the school filter.
  schools?: DistrictSchool[];
}

// Server-driven roster filters. The search box is debounced so typing does
// not fire a request per keystroke; every other control emits immediately.
export function RosterFilters({ value, onChange, schools }: RosterFiltersProps) {
  const [search, setSearch] = useState(value.q);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Drop any pending debounce on unmount so it cannot write to a gone page.
  useEffect(
    () => () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    },
    []
  );

  const handleSearch = (next: string) => {
    setSearch(next);
    if (timerRef.current) clearTimeout(timerRef.current);
    timerRef.current = setTimeout(() => onChange({ q: next }), SEARCH_DEBOUNCE_MS);
  };

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <Input
        id="educator-students-search"
        type="search"
        label="Search students"
        placeholder="Name or student ID"
        value={search}
        onChange={(e) => handleSearch(e.target.value)}
        data-testid="educator-students-search"
      />

      {schools && (
        <SchoolFilter
          schools={schools}
          value={value.schoolId}
          onChange={(schoolId) => onChange({ schoolId })}
        />
      )}

      <Select
        id="educator-students-status-filter"
        label="Status"
        value={value.status}
        onChange={(e) => onChange({ status: e.target.value as StudentStatusFilter })}
        data-testid="educator-students-status-filter"
      >
        {STUDENT_STATUSES.map((status) => (
          <option key={status} value={status}>
            {STUDENT_STATUS_LABELS[status]}
          </option>
        ))}
        <option value="All">All</option>
      </Select>

      <Select
        id="educator-students-grade-filter"
        label="Grade"
        value={value.grade}
        onChange={(e) => onChange({ grade: e.target.value as GradeLevel | '' })}
        data-testid="educator-students-grade-filter"
      >
        <option value="">All grades</option>
        {GRADE_LEVELS.map((grade) => (
          <option key={grade} value={grade}>
            {GRADE_LEVEL_LABELS[grade]}
          </option>
        ))}
      </Select>
    </div>
  );
}
