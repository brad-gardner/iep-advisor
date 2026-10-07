import i18n from './i18n';

/**
 * Human-facing label for a document's processing-pipeline status
 * (`"created"`/`"uploaded"`/`"processing"`/`"parsed"`/`"error"`) — shared by
 * `IepDocument`, `EtrDocument` and `ProgressReport`, all of which use this
 * same small, stable lowercase set. Lives in `iep-documents:documentStatus.*`
 * (the namespace of its first caller) rather than `common` (a new key there
 * is outside this phase's assignment; see docs/i18n/README.md). A caller
 * outside iep-documents must include `'iep-documents'` in its own
 * `useTranslation` array so the namespace's Spanish data is loaded before
 * this plain function (not a hook) is called from render. An unrecognized
 * value falls back to itself.
 */
export function documentStatusLabel(status: string): string {
  return i18n.t(`iep-documents:documentStatus.${status}`, { defaultValue: status });
}
