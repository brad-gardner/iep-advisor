import type { TemplateFieldDto } from '@/features/admin/templates/types';

/** Props shared by every read-mode (`ReadXxx`) field renderer: just the field
 *  definition and its current value — read views never save. */
export interface ReadFieldRendererProps {
  field: TemplateFieldDto;
  value: unknown;
}

/** Muted "nothing here yet" placeholder, shared across every read renderer so
 *  an empty field reads consistently whichever type it is. */
export const NOT_SET_LABEL = 'Not set';
