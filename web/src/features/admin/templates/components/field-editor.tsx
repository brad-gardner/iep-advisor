import { useState } from 'react';
import { ChevronDown, ChevronUp, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { useAutosave } from '@/features/iep-authoring/hooks/use-autosave';
import type { FieldType, TemplateFieldDto } from '../types';
import {
  defaultConfig,
  parseConfig,
  serializeConfig,
  validateConfig,
  type FieldConfig,
} from '../template-config';
import type { TemplateBuilder } from '../hooks/use-template-builder';
import { AutosaveIndicator } from './autosave-indicator';
import { SelectOptionsEditor } from './config-editors/select-options-editor';
import { TableColumnsEditor } from './config-editors/table-columns-editor';
import { DateConfigEditor, TextConfigEditor } from './config-editors/scalar-config-editors';

const FIELD_TYPES: FieldType[] = ['Text', 'RichText', 'Date', 'Select', 'Checkbox', 'Table'];

interface FieldEditorProps {
  field: TemplateFieldDto;
  builder: TemplateBuilder;
  readOnly: boolean;
  canMoveUp: boolean;
  canMoveDown: boolean;
  onMoveUp: () => void;
  onMoveDown: () => void;
}

/**
 * Edits one field. Label / type / required / config all autosave (debounced) via
 * one field PUT, gated on client-side config validity so we never persist an
 * invalid config. Local state is seeded once from `field`; the parent keys this
 * component by field id so edits survive tree replacements after each save.
 */
export function FieldEditor({
  field,
  builder,
  readOnly,
  canMoveUp,
  canMoveDown,
  onMoveUp,
  onMoveDown,
}: FieldEditorProps) {
  const [label, setLabel] = useState(field.label);
  const [fieldType, setFieldType] = useState<FieldType>(field.fieldType);
  const [required, setRequired] = useState(field.required);
  const [config, setConfig] = useState<FieldConfig>(() => parseConfig(field.fieldType, field.configJson));
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const autosave = useAutosave<void>(async () => {
    if (validateConfig(config)) return; // guarded — never PUT an invalid config
    const result = await builder.updateField(field.id, {
      fieldType,
      label,
      required,
      configJson: serializeConfig(config),
    });
    if (!result.ok) throw new Error(result.message ?? 'Save failed');
  });

  const scheduleIfValid = (cfg: FieldConfig) => {
    if (!validateConfig(cfg)) autosave.save(undefined);
  };

  const handleLabel = (value: string) => {
    setLabel(value);
    if (value.trim()) scheduleIfValid(config); // don't PUT an empty label
  };

  const handleType = (value: FieldType) => {
    const next = defaultConfig(value);
    setFieldType(value);
    setConfig(next);
    scheduleIfValid(next);
  };

  const handleRequired = (value: boolean) => {
    setRequired(value);
    scheduleIfValid(config);
  };

  const handleConfig = (next: FieldConfig) => {
    setConfig(next);
    scheduleIfValid(next);
  };

  const handleDelete = async () => {
    setDeleting(true);
    setDeleteError(null);
    autosave.cancel(); // drop any pending PUT so it can't race the DELETE
    const result = await builder.deleteField(field.id);
    setDeleting(false);
    if (result.ok) setConfirmDelete(false);
    else setDeleteError(result.message ?? 'Failed to delete field.');
  };

  const configError = validateConfig(config);

  return (
    <div className="rounded-card border border-brand-slate-200 bg-brand-slate-50 p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <AutosaveIndicator status={autosave.status} />
        {!readOnly && (
          <div className="flex items-center gap-1">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={onMoveUp}
              disabled={!canMoveUp || builder.isMutating}
              aria-label="Move field up"
              data-testid={`field-${field.id}-move-up`}
            >
              <ChevronUp size={14} strokeWidth={1.8} aria-hidden="true" />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={onMoveDown}
              disabled={!canMoveDown || builder.isMutating}
              aria-label="Move field down"
              data-testid={`field-${field.id}-move-down`}
            >
              <ChevronDown size={14} strokeWidth={1.8} aria-hidden="true" />
            </Button>
            <Button
              type="button"
              variant="danger"
              size="sm"
              onClick={() => setConfirmDelete(true)}
              aria-label="Delete field"
              data-testid={`field-${field.id}-delete`}
            >
              <Trash2 size={14} strokeWidth={1.8} aria-hidden="true" />
            </Button>
          </div>
        )}
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <Input
          label="Field label"
          id={`field-${field.id}-label`}
          value={label}
          onChange={(e) => handleLabel(e.target.value)}
          disabled={readOnly}
          data-testid={`field-${field.id}-label`}
        />
        <Select
          label="Field type"
          id={`field-${field.id}-type`}
          value={fieldType}
          onChange={(e) => handleType(e.target.value as FieldType)}
          disabled={readOnly}
          data-testid={`field-${field.id}-type`}
        >
          {FIELD_TYPES.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </Select>
      </div>

      <label className="mt-3 flex items-center gap-2 text-[13px] font-medium text-brand-slate-600">
        <input
          type="checkbox"
          checked={required}
          onChange={(e) => handleRequired(e.target.checked)}
          disabled={readOnly}
          className="h-4 w-4 rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-400"
          data-testid={`field-${field.id}-required`}
        />
        Required
      </label>

      {/* Type-specific config sub-editor. */}
      <div className="mt-4">
        {config.kind === 'Text' && (
          <div className="max-w-[12rem]">
            <TextConfigEditor
              idPrefix={`field-${field.id}`}
              config={config.text}
              onChange={(text) => handleConfig({ kind: 'Text', text })}
              disabled={readOnly}
            />
          </div>
        )}
        {config.kind === 'Date' && (
          <div className="max-w-[16rem]">
            <DateConfigEditor
              idPrefix={`field-${field.id}`}
              config={config.date}
              onChange={(date) => handleConfig({ kind: 'Date', date })}
              disabled={readOnly}
            />
          </div>
        )}
        {config.kind === 'Select' && (
          <SelectOptionsEditor
            idPrefix={`field-${field.id}`}
            options={config.select.options}
            onChange={(options) => handleConfig({ kind: 'Select', select: { options } })}
            disabled={readOnly}
          />
        )}
        {config.kind === 'Table' && (
          <TableColumnsEditor
            idPrefix={`field-${field.id}`}
            config={config.table}
            onChange={(table) => handleConfig({ kind: 'Table', table })}
            disabled={readOnly}
          />
        )}
        {(config.kind === 'RichText' || config.kind === 'Checkbox') && (
          <p className="text-xs text-brand-slate-400">This field type has no extra configuration.</p>
        )}
      </div>

      {configError && !readOnly && (
        <p className="mt-2 text-xs text-brand-danger-700" role="alert" data-testid={`field-${field.id}-config-error`}>
          {configError} Changes won&rsquo;t be saved until this is fixed.
        </p>
      )}

      <ConfirmDialog
        open={confirmDelete}
        title="Delete field"
        message={`Delete "${label.trim() || 'this field'}"? This cannot be undone.`}
        confirmLabel="Delete field"
        loading={deleting}
        error={deleteError}
        onConfirm={handleDelete}
        onCancel={() => {
          setConfirmDelete(false);
          setDeleteError(null);
        }}
        data-testid={`field-${field.id}-delete-confirm`}
      />
    </div>
  );
}
