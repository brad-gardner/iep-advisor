import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { SelectOption } from '../../template-config';

interface SelectOptionsEditorProps {
  options: SelectOption[];
  onChange: (options: SelectOption[]) => void;
  disabled?: boolean;
  /** Prefix for input ids/testids so nested (table-column) editors stay unique. */
  idPrefix: string;
}

/** Manage a Select field's (or Select column's) option list: value + optional label. */
export function SelectOptionsEditor({ options, onChange, disabled, idPrefix }: SelectOptionsEditorProps) {
  const update = (index: number, patch: Partial<SelectOption>) => {
    onChange(options.map((o, i) => (i === index ? { ...o, ...patch } : o)));
  };
  const remove = (index: number) => onChange(options.filter((_, i) => i !== index));
  const add = () => onChange([...options, { value: '' }]);

  return (
    <fieldset className="space-y-2" disabled={disabled}>
      <legend className="text-[13px] font-medium text-brand-slate-600">Options</legend>
      {options.map((opt, i) => (
        <div key={i} className="flex items-end gap-2">
          <div className="flex-1">
            <Input
              label="Value"
              id={`${idPrefix}-option-value-${i}`}
              value={opt.value}
              onChange={(e) => update(i, { value: e.target.value })}
              placeholder="e.g. yes"
              data-testid={`${idPrefix}-option-value-${i}`}
            />
          </div>
          <div className="flex-1">
            <Input
              label="Label (optional)"
              id={`${idPrefix}-option-label-${i}`}
              value={opt.label ?? ''}
              onChange={(e) => update(i, { label: e.target.value })}
              placeholder="e.g. Yes"
              data-testid={`${idPrefix}-option-label-${i}`}
            />
          </div>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => remove(i)}
            disabled={disabled || options.length <= 1}
            aria-label={`Remove option ${i + 1}`}
            data-testid={`${idPrefix}-option-remove-${i}`}
          >
            <Trash2 size={14} strokeWidth={1.8} aria-hidden="true" />
          </Button>
        </div>
      ))}
      <Button
        type="button"
        variant="secondary"
        size="sm"
        onClick={add}
        disabled={disabled}
        data-testid={`${idPrefix}-option-add`}
      >
        <Plus size={14} strokeWidth={1.8} className="mr-1" aria-hidden="true" />
        Add option
      </Button>
    </fieldset>
  );
}
