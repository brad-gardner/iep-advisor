import { useEffect, useEffectEvent, useState } from 'react';
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
  // The last `q` this box sent up, and the last URL `q` it has seen. When the
  // URL changes from outside (sidebar link, deep link while mounted) the box
  // adopts it — unless the URL is merely catching up with what was typed.
  const [emitted, setEmitted] = useState(value.q);
  const [seenQ, setSeenQ] = useState(value.q);
  if (value.q !== seenQ) {
    setSeenQ(value.q);
    if (value.q !== emitted && value.q !== search.trim()) setSearch(value.q);
  }

  // Always calls the *latest* onChange, so a filter changed inside the
  // debounce window is merged into, not overwritten by, the search patch.
  const emit = useEffectEvent((q: string) => {
    setEmitted(q.trim());
    onChange({ q });
  });

  useEffect(() => {
    if (search.trim() === value.q) return;
    const timer = setTimeout(() => emit(search), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search, value.q]);

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <Input
        id="educator-students-search"
        type="search"
        label="Search students"
        placeholder="Name or student ID"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        data-testid="educator-students-search"
      />

      {schools && (
        <SchoolFilter
          schools={schools}
          value={value.schoolId ? String(value.schoolId) : ''}
          onChange={(schoolId) => onChange({ schoolId: schoolId ? Number(schoolId) : null })}
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
