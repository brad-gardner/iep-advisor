import type { TemplateFieldDto } from '@/features/admin/templates/types';

/** Props shared by every read-mode (`ReadXxx`) field renderer: just the field
 *  definition and its current value — read views never save. */
export interface ReadFieldRendererProps {
  field: TemplateFieldDto;
  value: unknown;
  /** Requests that the field switch to edit mode, focused on a specific row
   *  (a goal card's "Edit goal") — `SectionCard` wires this to opening the
   *  section and threading the row through as `initialFocusRowKey`. Undefined
   *  outside an editable `SectionCard` (e.g. a frozen version's read view),
   *  where every read renderer other than `ReadGoals` already ignores it. */
  onEditRow?: (rowKey: string) => void;
  /** True when this is the only field in its section — the section's own
   *  heading already names it, so a renderer whose label is purely a repeated
   *  field name (not one that also carries a count/total, like the Table
   *  renderers) hides it. Renderers for which that doesn't apply ignore this. */
  hideLabel?: boolean;
}

/** Muted "nothing here yet" placeholder, shared across every read renderer so
 *  an empty field reads consistently whichever type it is. */
export const NOT_SET_LABEL = 'Not set';
