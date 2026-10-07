// Grade Level / Disability Category dropdown options for the parent-side
// child profile form (`child-form.tsx`).
//
// `ChildProfile.gradeLevel` / `disabilityCategory` are plain free-text
// strings in the API contract (see `CreateChildProfileRequest` /
// `UpdateChildProfileRequest` in `@/types/api`) — unlike the educator side's
// `GradeLevel`/`DisabilityCategory` enums. We still want parents picking from
// a fixed list of readable values, while never silently discarding whatever
// text already lives on an existing child's record. This module builds that
// dropdown-options layer on top of the pure normalization logic in
// `@/lib/child-profile-normalize.ts` (moved there so the lib-level label
// helpers below can depend on it without a lib→feature→lib cycle):
//
//   - `GRADE_LEVEL_OPTIONS` / `DISABILITY_CATEGORY_OPTIONS` are the canonical,
//     ordered, readable strings stored on submit.
//   - `normalizeGradeLevel` / `normalizeDisabilityCategory` (re-exported from
//     `child-profile-normalize.ts`) map a stored (or legacy) value to its
//     canonical option using case-insensitive, whitespace-tolerant matching —
//     including a few safe aliases. A value that matches nothing is returned
//     unchanged (trimmed) rather than dropped.
//   - `buildGradeLevelOptions` / `buildDisabilityCategoryOptions` turn that
//     into the full `<option>` list for a given current value, appending an
//     extra "(current value)" entry when the current value didn't match any
//     canonical option, so re-submitting without touching the field leaves
//     an unmatched stored string unchanged. A matched variant ("8", "SLD")
//     is saved back as its canonical option — same meaning, tidier text.
import {
  DISABILITY_CATEGORIES,
  DISABILITY_CATEGORY_LABELS,
} from "@/features/educator/types";
import i18n from "@/lib/i18n";
import { gradeLevelLabel } from "@/lib/grade-level-label";
import { disabilityCategoryLabel } from "@/lib/disability-category-label";
import {
  ordinal,
  normalizeGradeLevel,
  normalizeDisabilityCategory,
  SERVER_DISPLAY_LABELS,
} from "@/lib/child-profile-normalize";

export { normalizeGradeLevel, normalizeDisabilityCategory, SERVER_DISPLAY_LABELS };

export interface ChildProfileOption {
  value: string;
  label: string;
}

// ---------------------------------------------------------------- Grade level

export const GRADE_LEVEL_OPTIONS: readonly string[] = [
  "Pre-K",
  "Kindergarten",
  ...Array.from({ length: 12 }, (_, i) => ordinal(i + 1)),
  "Ungraded",
];

export function buildGradeLevelOptions(
  currentValue: string | null | undefined,
): ChildProfileOption[] {
  return buildOptions(
    GRADE_LEVEL_OPTIONS,
    normalizeGradeLevel(currentValue),
    gradeLevelLabel,
  );
}

// ------------------------------------------------------------ Disability category

// Readable labels, in the same order as `DISABILITY_CATEGORIES` — the 14 IDEA
// categories plus "Other". Reused from the educator feature rather than
// duplicated so the two surfaces can't drift.
export const DISABILITY_CATEGORY_OPTIONS: readonly string[] =
  DISABILITY_CATEGORIES.map((category) => DISABILITY_CATEGORY_LABELS[category]);

export function buildDisabilityCategoryOptions(
  currentValue: string | null | undefined,
): ChildProfileOption[] {
  return buildOptions(
    DISABILITY_CATEGORY_OPTIONS,
    normalizeDisabilityCategory(currentValue),
    disabilityCategoryLabel,
  );
}

// ---------------------------------------------------------------- Shared

function buildOptions(
  canonicalOptions: readonly string[],
  normalizedCurrentValue: string,
  labelFor: (value: string) => string,
): ChildProfileOption[] {
  const options: ChildProfileOption[] = [
    { value: "", label: i18n.t("common:ui.notSet") },
    ...canonicalOptions.map((value) => ({ value, label: labelFor(value) })),
  ];

  if (
    normalizedCurrentValue &&
    !canonicalOptions.includes(normalizedCurrentValue)
  ) {
    options.push({
      value: normalizedCurrentValue,
      label: i18n.t("common:ui.currentValueSuffix", {
        value: normalizedCurrentValue,
      }),
    });
  }

  return options;
}
