import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation('admin');
  return (
    <Input
      type="number"
      min={1}
      label={t('templates.configEditors.maxLengthLabel')}
      id={`${idPrefix}-maxlength`}
      value={config.maxLength ?? ''}
      onChange={(e) => {
        const n = e.target.value === '' ? undefined : Number(e.target.value);
        onChange({ maxLength: n != null && Number.isFinite(n) && n > 0 ? n : undefined });
      }}
      placeholder={t('templates.configEditors.maxLengthPlaceholder')}
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
  const { t } = useTranslation('admin');
  return (
    <Input
      label={t('templates.configEditors.dateFormatLabel')}
      id={`${idPrefix}-format`}
      value={config.format ?? ''}
      onChange={(e) => onChange({ format: e.target.value || undefined })}
      placeholder={t('templates.configEditors.dateFormatPlaceholder')}
      disabled={disabled}
      data-testid={`${idPrefix}-format`}
    />
  );
}
