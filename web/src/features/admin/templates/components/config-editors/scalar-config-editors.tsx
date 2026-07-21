import { Input } from '@/components/ui/input';
import type { DateConfig, TextConfig } from '../../template-config';

interface TextConfigEditorProps {
  config: TextConfig;
  onChange: (config: TextConfig) => void;
  disabled?: boolean;
  idPrefix: string;
}

/** Text field config: optional max length. */
export function TextConfigEditor({ config, onChange, disabled, idPrefix }: TextConfigEditorProps) {
  return (
    <Input
      type="number"
      min={1}
      label="Max length (optional)"
      id={`${idPrefix}-maxlength`}
      value={config.maxLength ?? ''}
      onChange={(e) => {
        const n = e.target.value === '' ? undefined : Number(e.target.value);
        onChange({ maxLength: n != null && Number.isFinite(n) && n > 0 ? n : undefined });
      }}
      placeholder="No limit"
      disabled={disabled}
      data-testid={`${idPrefix}-maxlength`}
    />
  );
}

interface DateConfigEditorProps {
  config: DateConfig;
  onChange: (config: DateConfig) => void;
  disabled?: boolean;
  idPrefix: string;
}

/** Date field config: optional display format string. */
export function DateConfigEditor({ config, onChange, disabled, idPrefix }: DateConfigEditorProps) {
  return (
    <Input
      label="Date format (optional)"
      id={`${idPrefix}-format`}
      value={config.format ?? ''}
      onChange={(e) => onChange({ format: e.target.value || undefined })}
      placeholder="e.g. MM/dd/yyyy"
      disabled={disabled}
      data-testid={`${idPrefix}-format`}
    />
  );
}
