/**
 * Turns a compliance-board `drill` fragment (e.g. `"attention=OverdueAnnual"`)
 * into a roster link, optionally scoped to a school.
 *
 * Deviation from the plan5 contract's literal wording (`&schoolId=`): the
 * roster's actual URL param is `school`, not `schoolId` (see
 * `features/educator/hooks/use-roster-query.ts`) — sending `schoolId` would be
 * silently ignored and the link would show every school's matching students
 * instead of the filtered one. This uses the working param name so the
 * drilldown row counts actually match what the roster then shows.
 */
export function districtDrillHref(
  drill: Record<string, string> | undefined,
  key: string,
  schoolId?: number | null
): string {
  const base = drill?.[key] ?? '';
  const params = new URLSearchParams(base);
  if (schoolId != null) params.set('school', String(schoolId));
  return `/educator/students?${params.toString()}`;
}
