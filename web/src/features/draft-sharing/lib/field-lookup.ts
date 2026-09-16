import type { TemplateVersionDetailDto } from '@/features/admin/templates/types';

export interface FieldLocation {
  fieldId: number;
  sectionId: number;
}

/** Maps a template field's stable `fieldKey` (guid) to its numeric id + owning
 *  section id, so a response's `targetFieldKey` can drive `jumpToField`
 *  (which needs the DOM id built from the numeric field id). */
export function buildFieldLocationLookup(templateVersion: TemplateVersionDetailDto): Map<string, FieldLocation> {
  const map = new Map<string, FieldLocation>();
  for (const section of templateVersion.sections) {
    for (const field of section.fields) {
      map.set(field.fieldKey, { fieldId: field.id, sectionId: section.id });
    }
  }
  return map;
}
