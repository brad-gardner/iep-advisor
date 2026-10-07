import i18n from '@/lib/i18n';
import type { ColumnSemantic, FieldSemantic } from '../document-semantics';

/**
 * Translated label for a template field's `semantic` tag, shown in the
 * "Semantic (what this field means)" dropdown (`field-editor.tsx`). Lives
 * here rather than in `document-semantics.ts` itself because that file sits
 * on an eagerly-reachable import path (`features/shared-drafts` imports its
 * TYPES) — this file, imported only by `features/admin/templates`' own
 * (lazy, platform-admin-only) components, is not. See
 * `lib/i18n/staff-namespace-boundary.test.ts` and `document-semantics.ts`'s
 * own doc comment.
 *
 * `FieldSemantic` is a closed, exhaustive union (mirrors the backend
 * allow-list), so every case below is a real key in `admin:fieldSemantic.*`
 * — no fallback needed for an "unrecognized" value the way
 * `disabilityCategoryLabel` needs one for free-text legacy data.
 */
export function fieldSemanticLabel(semantic: FieldSemantic): string {
  switch (semantic) {
    case 'studentProfile':
      return i18n.t('admin:templates.fieldSemantic.studentProfile');
    case 'presentLevels':
      return i18n.t('admin:templates.fieldSemantic.presentLevels');
    case 'eligibility':
      return i18n.t('admin:templates.fieldSemantic.eligibility');
    case 'placement':
      return i18n.t('admin:templates.fieldSemantic.placement');
    case 'progressMonitoring':
      return i18n.t('admin:templates.fieldSemantic.progressMonitoring');
    case 'specialFactors':
      return i18n.t('admin:templates.fieldSemantic.specialFactors');
    case 'goals':
      return i18n.t('admin:templates.fieldSemantic.goals');
    case 'services':
      return i18n.t('admin:templates.fieldSemantic.services');
    case 'accommodations':
      return i18n.t('admin:templates.fieldSemantic.accommodations');
    case 'transition':
      return i18n.t('admin:templates.fieldSemantic.transition');
    case 'futurePlanning':
      return i18n.t('admin:templates.fieldSemantic.futurePlanning');
    case 'extendedSchoolYear':
      return i18n.t('admin:templates.fieldSemantic.extendedSchoolYear');
    case 'testing':
      return i18n.t('admin:templates.fieldSemantic.testing');
    case 'transportation':
      return i18n.t('admin:templates.fieldSemantic.transportation');
    case 'lre':
      return i18n.t('admin:templates.fieldSemantic.lre');
    case 'participants':
      return i18n.t('admin:templates.fieldSemantic.participants');
    case 'signatures':
      return i18n.t('admin:templates.fieldSemantic.signatures');
    case 'referralReason':
      return i18n.t('admin:templates.fieldSemantic.referralReason');
    case 'evaluationPlan':
      return i18n.t('admin:templates.fieldSemantic.evaluationPlan');
    case 'evaluatorReports':
      return i18n.t('admin:templates.fieldSemantic.evaluatorReports');
    case 'teamSummary':
      return i18n.t('admin:templates.fieldSemantic.teamSummary');
    case 'eligibilityDetermination':
      return i18n.t('admin:templates.fieldSemantic.eligibilityDetermination');
    case 'meetingDate':
      return i18n.t('admin:templates.fieldSemantic.meetingDate');
    case 'effectiveDates':
      return i18n.t('admin:templates.fieldSemantic.effectiveDates');
  }
}

/** Translated label for a table column's `semantic` tag — same shape as
 *  `fieldSemanticLabel` above, for `ColumnSemantic`. */
export function columnSemanticLabel(semantic: ColumnSemantic): string {
  switch (semantic) {
    case 'domain':
      return i18n.t('admin:templates.columnSemantic.domain');
    case 'goalText':
      return i18n.t('admin:templates.columnSemantic.goalText');
    case 'baseline':
      return i18n.t('admin:templates.columnSemantic.baseline');
    case 'targetCriteria':
      return i18n.t('admin:templates.columnSemantic.targetCriteria');
    case 'measurementMethod':
      return i18n.t('admin:templates.columnSemantic.measurementMethod');
    case 'timeframe':
      return i18n.t('admin:templates.columnSemantic.timeframe');
    case 'serviceType':
      return i18n.t('admin:templates.columnSemantic.serviceType');
    case 'frequency':
      return i18n.t('admin:templates.columnSemantic.frequency');
    case 'duration':
      return i18n.t('admin:templates.columnSemantic.duration');
    case 'location':
      return i18n.t('admin:templates.columnSemantic.location');
    case 'providerRole':
      return i18n.t('admin:templates.columnSemantic.providerRole');
    case 'startDate':
      return i18n.t('admin:templates.columnSemantic.startDate');
    case 'endDate':
      return i18n.t('admin:templates.columnSemantic.endDate');
    case 'category':
      return i18n.t('admin:templates.columnSemantic.category');
    case 'accommodation':
      return i18n.t('admin:templates.columnSemantic.accommodation');
    case 'goalArea':
      return i18n.t('admin:templates.columnSemantic.goalArea');
    case 'transitionServices':
      return i18n.t('admin:templates.columnSemantic.transitionServices');
    case 'participantName':
      return i18n.t('admin:templates.columnSemantic.participantName');
    case 'participantRole':
      return i18n.t('admin:templates.columnSemantic.participantRole');
    case 'attended':
      return i18n.t('admin:templates.columnSemantic.attended');
    case 'evaluationDomain':
      return i18n.t('admin:templates.columnSemantic.evaluationDomain');
    case 'evaluatorName':
      return i18n.t('admin:templates.columnSemantic.evaluatorName');
    case 'findings':
      return i18n.t('admin:templates.columnSemantic.findings');
  }
}

/** Translated label for a template field's `fieldType` (`admin:templates.fieldType.*`) —
 *  shared by `field-editor.tsx`'s field-type select and
 *  `table-columns-editor.tsx`'s column-type select (a strict subset of the
 *  same vocabulary: `Text`/`Date`/`Select`/`Checkbox`). */
export function fieldTypeLabel(type: 'Text' | 'RichText' | 'Date' | 'Select' | 'Checkbox' | 'Table'): string {
  switch (type) {
    case 'Text':
      return i18n.t('admin:templates.fieldType.Text');
    case 'RichText':
      return i18n.t('admin:templates.fieldType.RichText');
    case 'Date':
      return i18n.t('admin:templates.fieldType.Date');
    case 'Select':
      return i18n.t('admin:templates.fieldType.Select');
    case 'Checkbox':
      return i18n.t('admin:templates.fieldType.Checkbox');
    case 'Table':
      return i18n.t('admin:templates.fieldType.Table');
  }
}
