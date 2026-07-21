import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import {
  newTableColumn,
  readColumnOptions,
  writeColumnOptions,
  type TableColumn,
  type TableColumnType,
  type TableConfig,
} from '../../template-config';
import { SelectOptionsEditor } from './select-options-editor';

const COLUMN_TYPES: TableColumnType[] = ['Text', 'Date', 'Select', 'Checkbox'];

interface TableColumnsEditorProps {
  config: TableConfig;
  onChange: (config: TableConfig) => void;
  disabled?: boolean;
  idPrefix: string;
}

/** Manage a Table field's columns (no nested Table/RichText) plus row bounds. */
export function TableColumnsEditor({ config, onChange, disabled, idPrefix }: TableColumnsEditorProps) {
  const { columns, minRows, maxRows } = config;

  const updateColumn = (index: number, patch: Partial<TableColumn>) => {
    onChange({ ...config, columns: columns.map((c, i) => (i === index ? { ...c, ...patch } : c)) });
  };
  const removeColumn = (index: number) => {
    onChange({ ...config, columns: columns.filter((_, i) => i !== index) });
  };
  const addColumn = () => onChange({ ...config, columns: [...columns, newTableColumn()] });

  const changeType = (index: number, type: TableColumnType) => {
    // Reset per-type config on type change; only Select columns carry configJson.
    updateColumn(index, {
      type,
      configJson: type === 'Select' ? writeColumnOptions([{ value: '' }]) : undefined,
    });
  };

  const numberOrUndefined = (raw: string) => {
    if (raw === '') return undefined;
    const n = Number(raw);
    return Number.isFinite(n) && n >= 0 ? n : undefined;
  };

  return (
    <div className="space-y-4">
      <fieldset className="space-y-3" disabled={disabled}>
        <legend className="text-[13px] font-medium text-brand-slate-600">Columns</legend>
        {columns.map((col, i) => {
          const colId = `${idPrefix}-col-${i}`;
          return (
            <div key={col.columnKey} className="rounded-card border border-brand-slate-200 p-3 space-y-3">
              <div className="flex items-end gap-2">
                <div className="flex-1">
                  <Input
                    label="Column label"
                    id={`${colId}-label`}
                    value={col.label}
                    onChange={(e) => updateColumn(i, { label: e.target.value })}
                    placeholder="e.g. Goal"
                    data-testid={`${colId}-label`}
                  />
                </div>
                <div className="w-32">
                  <Select
                    label="Type"
                    id={`${colId}-type`}
                    value={col.type}
                    onChange={(e) => changeType(i, e.target.value as TableColumnType)}
                    data-testid={`${colId}-type`}
                  >
                    {COLUMN_TYPES.map((t) => (
                      <option key={t} value={t}>
                        {t}
                      </option>
                    ))}
                  </Select>
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => removeColumn(i)}
                  disabled={disabled || columns.length <= 1}
                  aria-label={`Remove column ${i + 1}`}
                  data-testid={`${colId}-remove`}
                >
                  <Trash2 size={14} strokeWidth={1.8} aria-hidden="true" />
                </Button>
              </div>

              <label className="flex items-center gap-2 text-[13px] font-medium text-brand-slate-600">
                <input
                  type="checkbox"
                  checked={col.required}
                  onChange={(e) => updateColumn(i, { required: e.target.checked })}
                  disabled={disabled}
                  className="h-4 w-4 rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-400"
                  data-testid={`${colId}-required`}
                />
                Required
              </label>

              {col.type === 'Select' && (
                <SelectOptionsEditor
                  idPrefix={colId}
                  options={readColumnOptions(col.configJson)}
                  onChange={(opts) => updateColumn(i, { configJson: writeColumnOptions(opts) })}
                  disabled={disabled}
                />
              )}
            </div>
          );
        })}
        <Button
          type="button"
          variant="secondary"
          size="sm"
          onClick={addColumn}
          disabled={disabled}
          data-testid={`${idPrefix}-col-add`}
        >
          <Plus size={14} strokeWidth={1.8} className="mr-1" aria-hidden="true" />
          Add column
        </Button>
      </fieldset>

      <div className="flex gap-3">
        <div className="w-32">
          <Input
            type="number"
            min={0}
            label="Min rows"
            id={`${idPrefix}-minrows`}
            value={minRows ?? ''}
            onChange={(e) => onChange({ ...config, minRows: numberOrUndefined(e.target.value) })}
            placeholder="None"
            disabled={disabled}
            data-testid={`${idPrefix}-minrows`}
          />
        </div>
        <div className="w-32">
          <Input
            type="number"
            min={0}
            label="Max rows"
            id={`${idPrefix}-maxrows`}
            value={maxRows ?? ''}
            onChange={(e) => onChange({ ...config, maxRows: numberOrUndefined(e.target.value) })}
            placeholder="None"
            disabled={disabled}
            data-testid={`${idPrefix}-maxrows`}
          />
        </div>
      </div>
    </div>
  );
}
