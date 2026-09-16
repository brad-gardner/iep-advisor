import type { AttentionFilter } from '@/features/educator/types';

/**
 * A roster link for a home-page stat tile: `/educator/students?attention=X`
 * (optionally `&school=Y`). `AttentionFilter` enum names are accepted verbatim
 * by `useRosterQuery`'s `attention` param (see `parseAttention`), so this needs
 * no server-provided mapping — unlike the compliance board's `drill` map
 * (`features/district-admin/lib/drill-link.ts`), which server-composes the
 * query string for us.
 *
 * Deviation from the plan5 contract's literal wording (`&schoolId=`): the
 * roster's actual URL param is `school` (see `use-roster-query.ts`) — `schoolId`
 * is not read there, so using it would silently fail to scope the link.
 */
export function rosterAttentionHref(attention: AttentionFilter, schoolId?: number | null): string {
  const params = new URLSearchParams({ attention });
  if (schoolId != null) params.set('school', String(schoolId));
  return `/educator/students?${params.toString()}`;
}
