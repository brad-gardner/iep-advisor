import i18n from '@/lib/i18n';

/**
 * Translated label for a bare analysis source type (`"IEP"`, `"ETR"`,
 * `"Progress report"`) — via `analysis:sourceLabel.sourceType.*`. Used as
 * the `"<type> #<id>"` fallback by `RunSourceSections` (this feature) and
 * by `otherSourceLabel` in `iep-documents`'/`etr-documents`' own analysis
 * tabs, for a source whose own record the server couldn't resolve (so
 * there's no formatted `sourceLabel`/`label` to show instead). Same shape
 * as `meetingTypeLabel`: a plain function over `i18n.t`, callable from a
 * module-level helper as well as render bodies. A caller outside this
 * feature must include `'analysis'` in its own `useTranslation` array so
 * the namespace's Spanish data is loaded before this (non-hook) function is
 * called. An unrecognized type falls back to the raw value.
 */
export function analysisSourceTypeLabel(sourceType: string): string {
  return i18n.t(`analysis:sourceLabel.sourceType.${sourceType}`, { defaultValue: sourceType });
}
