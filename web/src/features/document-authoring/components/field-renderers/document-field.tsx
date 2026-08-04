import { CheckboxField } from './checkbox-field';
import { DateField } from './date-field';
import { RichTextField } from './rich-text-field';
import { SelectField } from './select-field';
import { TableField } from './table-field';
import { TextField } from './text-field';
import type { FieldRendererProps } from './types';

/** Dispatches a template field to its per-`FieldType` renderer. Exhaustive over
 *  the FieldType palette — a new type is a compile-time addition here. */
export function DocumentField(props: FieldRendererProps) {
  switch (props.field.fieldType) {
    case 'Text':
      return <TextField {...props} />;
    case 'RichText':
      return <RichTextField {...props} />;
    case 'Date':
      return <DateField {...props} />;
    case 'Select':
      return <SelectField {...props} />;
    case 'Checkbox':
      return <CheckboxField {...props} />;
    case 'Table':
      return <TableField {...props} />;
    default: {
      // Exhaustiveness guard: adding a FieldType without a renderer fails here.
      const _exhaustive: never = props.field.fieldType;
      return _exhaustive;
    }
  }
}
