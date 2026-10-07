import i18n from '@/lib/i18n';
import type { FieldSemantic } from '@/features/admin/templates/document-semantics';

/**
 * Translated replacement for `rowBlockItemLabel` (`@/features/admin/templates/
 * document-semantics`), which returns a raw, untranslated English noun ("Goal",
 * "Service", …) — fine for `admin/templates`' own surface (not yet converted),
 * but a real mixed-language bug when interpolated into otherwise-translated
 * document-authoring text (the editor's row header/remove button, and
 * `completeness.ts`'s owner advisory). Scoped to this feature, not
 * `admin/templates`, because every key below lives in the staff-only
 * `document-authoring` namespace (`rowBlock.*`) — see `docs/i18n/README.md`'s
 * "Staff and admin namespaces" and `app/lazy-routes/staff-locales.ts`.
 *
 * Only 'goals'/'services'/'accommodations'/'transition' get their own noun —
 * every other semantic (including 'participants'/'evaluatorReports', which
 * `ROW_BLOCK_SEMANTICS` also covers but never reach the generic row-block UI
 * these keys back, since goals/services render their own dedicated block
 * first — see `table-field.tsx`) falls back to the generic "row".
 */
export function rowBlockItemNoun(semantic: FieldSemantic | undefined): string {
  switch (semantic) {
    case 'goals':
      return i18n.t('document-authoring:rowBlock.itemNoun.goal');
    case 'services':
      return i18n.t('document-authoring:rowBlock.itemNoun.service');
    case 'accommodations':
      return i18n.t('document-authoring:rowBlock.itemNoun.accommodation');
    case 'transition':
      return i18n.t('document-authoring:rowBlock.itemNoun.transitionItem');
    default:
      return i18n.t('document-authoring:rowBlock.itemNoun.row');
  }
}

/**
 * Whole-sentence "Add {item}" per semantic, rather than one generic
 * "Add {{item}}" template interpolating `rowBlockItemNoun`'s bare noun —
 * a single shared template bakes in whatever article/agreement English
 * happens to need and assumes every language's phrasing follows the same
 * shape, which does not hold in general (same reasoning as
 * `docs/i18n/README.md`'s "mixed-language sentence" rule, just for a
 * translated-but-rigidly-templated sentence rather than a raw one).
 */
export function addRowItemLabel(semantic: FieldSemantic | undefined): string {
  switch (semantic) {
    case 'goals':
      return i18n.t('document-authoring:rowBlock.addGoal');
    case 'services':
      return i18n.t('document-authoring:rowBlock.addService');
    case 'accommodations':
      return i18n.t('document-authoring:rowBlock.addAccommodation');
    case 'transition':
      return i18n.t('document-authoring:rowBlock.addTransitionItem');
    default:
      return i18n.t('document-authoring:rowBlock.addRow');
  }
}

/** Whole-sentence "Remove {item} {number}" per semantic — same reasoning as
 *  `addRowItemLabel` above. */
export function removeRowItemLabel(semantic: FieldSemantic | undefined, number: number): string {
  switch (semantic) {
    case 'goals':
      return i18n.t('document-authoring:rowBlock.removeGoal', { number });
    case 'services':
      return i18n.t('document-authoring:rowBlock.removeService', { number });
    case 'accommodations':
      return i18n.t('document-authoring:rowBlock.removeAccommodation', { number });
    case 'transition':
      return i18n.t('document-authoring:rowBlock.removeTransitionItem', { number });
    default:
      return i18n.t('document-authoring:rowBlock.removeRow', { number });
  }
}
