import { ReadCheckbox } from './read-checkbox';
import { ReadDate } from './read-date';
import { ReadRichText } from './read-rich-text';
import { ReadSelect } from './read-select';
import { ReadTable } from './read-table';
import { ReadText } from './read-text';
import type { ReadFieldRendererProps } from './types';

/** Dispatches a template field to its per-`FieldType` read-mode renderer.
 *  Mirrors `DocumentField`'s exhaustiveness over the FieldType palette. */
export function ReadField(props: ReadFieldRendererProps) {
  switch (props.field.fieldType) {
    case 'Text':
      return <ReadText {...props} />;
    case 'RichText':
      return <ReadRichText {...props} />;
    case 'Date':
      return <ReadDate {...props} />;
    case 'Select':
      return <ReadSelect {...props} />;
    case 'Checkbox':
      return <ReadCheckbox {...props} />;
    case 'Table':
      return <ReadTable {...props} />;
    default: {
      // Exhaustiveness guard: adding a FieldType without a read renderer fails here.
      const _exhaustive: never = props.field.fieldType;
      return _exhaustive;
    }
  }
}
