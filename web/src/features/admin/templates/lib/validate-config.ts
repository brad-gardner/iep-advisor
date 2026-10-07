import i18n from '@/lib/i18n';
import { asRecord, parseSelectOptions, type FieldConfig, type SelectOption } from '../template-config';
import type { ColumnSemantic } from '../document-semantics';

/**
 * Client-side validity of a typed config, translated via the staff-only
 * `admin` namespace. Returns `null` when valid, else a human-readable
 * reason. Mirrors the backend's per-type rules so autosave can be gated (we
 * never PUT an invalid config) and the UI can hint inline.
 *
 * Lives in its own file, separate from `../template-config.ts`, because
 * that file sits on an eagerly-reachable import path (`features/shared-drafts`
 * imports `parseConfig` from it at runtime) while this one — imported only
 * by `field-editor.tsx` (lazy, platform-admin-only) — is not. See that
 * file's own doc comment and `lib/i18n/staff-namespace-boundary.test.ts`.
 */
export function validateConfig(config: FieldConfig): string | null {
  switch (config.kind) {
    case 'Text':
    case 'RichText':
    case 'Date':
    case 'Checkbox':
      return null;
    case 'Select':
      return validateSelectOptions(config.select.options);
    case 'Table': {
      const { columns, minRows, maxRows } = config.table;
      if (columns.length === 0) return i18n.t('admin:templates.validation.addColumn');
      if (columns.some((c) => c.label.trim() === '')) {
        return i18n.t('admin:templates.validation.columnLabelRequired');
      }
      const sems = columns.map((c) => c.semantic).filter((s): s is ColumnSemantic => s != null);
      if (new Set(sems).size !== sems.length) {
        return i18n.t('admin:templates.validation.columnSemanticsUnique');
      }
      for (const col of columns) {
        if (col.type === 'Select') {
          const opts = parseSelectOptions(asRecord(col.configJson).options);
          const err = validateSelectOptions(opts);
          if (err) {
            return i18n.t('admin:templates.validation.columnOptionError', {
              label: col.label.trim() || i18n.t('admin:templates.formPreview.untitledColumn'),
              error: err,
            });
          }
        }
      }
      if (minRows != null && maxRows != null && minRows > maxRows) {
        return i18n.t('admin:templates.validation.minMaxRows');
      }
      return null;
    }
  }
}

/** Non-empty unique-value check shared by Select fields and Select columns. */
function validateSelectOptions(options: SelectOption[]): string | null {
  const values = options.map((o) => o.value.trim());
  if (values.length === 0) return i18n.t('admin:templates.validation.addOption');
  if (values.some((v) => v === '')) return i18n.t('admin:templates.validation.optionValueRequired');
  if (new Set(values).size !== values.length) return i18n.t('admin:templates.validation.optionValuesUnique');
  return null;
}
