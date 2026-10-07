import { useEffect, useEffectEvent, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Input, Select } from '@/components/ui/input';
import type { DistrictSchool } from '@/features/district-admin/types';
import { gradeLevelLabel } from '@/lib/grade-level-label';
import { studentStatusLabel } from '../../lib/student-enum-labels';
import { GRADE_LEVELS, STUDENT_STATUSES } from '../../types';
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
  const { t } = useTranslation('educator');
  const [search, setSearch] = useState(value.q);
  // The last `q` this box sent up, and the last URL `q` it has seen. When the
  // URL changes from outside (sidebar link, deep link while mounted) the box
  // adopts it — unless the URL is merely catching up with what was typed.
  const [emitted, setEmitted] = useState(value.q);
  const [seenQ, setSeenQ] = useState(value.q);
  if (value.q !== seenQ) {
    setSeenQ(value.q);
    if (value.q !== emitted && value.q !== search.trim()) setSearch(value.q);
    // Whatever the URL now says is the new baseline — otherwise a history entry
    // whose q equals something typed earlier (Back after clearing) would be
    // refused and then debounced away.
    if (value.q !== emitted) setEmitted(value.q);
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
        label={t('rosterFilters.searchLabel')}
        placeholder={t('rosterFilters.searchPlaceholder')}
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
        label={t('rosterFilters.statusLabel')}
        value={value.status}
        onChange={(e) => onChange({ status: e.target.value as StudentStatusFilter })}
        data-testid="educator-students-status-filter"
      >
        {STUDENT_STATUSES.map((status) => (
          <option key={status} value={status}>
            {studentStatusLabel(status)}
          </option>
        ))}
        <option value="All">{t('rosterFilters.statusAll')}</option>
      </Select>

      <Select
        id="educator-students-grade-filter"
        label={t('rosterFilters.gradeLabel')}
        value={value.grade}
        onChange={(e) => onChange({ grade: e.target.value as GradeLevel | '' })}
        data-testid="educator-students-grade-filter"
      >
        <option value="">{t('rosterFilters.gradeAll')}</option>
        {GRADE_LEVELS.map((grade) => (
          <option key={grade} value={grade}>
            {gradeLevelLabel(grade)}
          </option>
        ))}
      </Select>
    </div>
  );
}
