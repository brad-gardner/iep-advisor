import { useEffect, useState } from 'react';
import { getDistrictDashboard } from '@/features/district-admin/api/district-api';

// Dashboard "needs attention" tiles deep-link to the roster with
// ?attention=<key>. The roster payload carries no team/linked-parent signal,
// so the ID set is sourced from the dashboard aggregate (admins only).
export const ATTENTION_LABELS: Record<string, string> = {
  'no-staff': 'no case manager',
  'no-parent': 'no linked parent',
};

// Resolves the student-ID set for an attention key. A recognised key with no
// matching dashboard data yields an empty set (filters to nothing), not an
// unfiltered roster, so the deep-link never silently shows everyone. `null`
// while unresolved or when no key is active.
export function useAttentionIds(attention: string | null, enabled: boolean): Set<number> | null {
  const [ids, setIds] = useState<{ key: string; ids: Set<number> } | null>(null);
  const key = enabled && attention && ATTENTION_LABELS[attention] ? attention : null;

  useEffect(() => {
    if (!key) return;
    let active = true;
    (async () => {
      try {
        const response = await getDistrictDashboard();
        if (!active) return;
        const data = response.success ? response.data : null;
        const source = key === 'no-staff' ? data?.studentsWithoutStaff : data?.studentsWithoutParent;
        setIds({ key, ids: new Set((source ?? []).map((s) => s.schoolStudentId)) });
      } catch {
        if (active) setIds({ key, ids: new Set() });
      }
    })();
    return () => {
      active = false;
    };
  }, [key]);

  return key && ids?.key === key ? ids.ids : null;
}
