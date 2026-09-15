import { useState } from 'react';
import type { SchoolStudent } from '../types';

// Cross-page checkbox selection for bulk roster actions. The key set survives
// paging; the page clears it when the filter set changes.
export function useRosterSelection() {
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());

  const toggle = (student: SchoolStudent) =>
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(student.id)) next.delete(student.id);
      else next.add(student.id);
      return next;
    });

  const toggleAll = (rows: SchoolStudent[], select: boolean) =>
    setSelectedIds((prev) => {
      const next = new Set(prev);
      rows.forEach((s) => (select ? next.add(s.id) : next.delete(s.id)));
      return next;
    });

  const clear = () => setSelectedIds(new Set());

  return { selectedIds, toggle, toggleAll, clear };
}
