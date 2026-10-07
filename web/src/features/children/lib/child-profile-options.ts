// Grade Level / Disability Category dropdown options for the parent-side
// child profile form (`child-form.tsx`).
//
// `ChildProfile.gradeLevel` / `disabilityCategory` are plain free-text
// strings in the API contract (see `CreateChildProfileRequest` /
// `UpdateChildProfileRequest` in `@/types/api`) — unlike the educator side's
// `GradeLevel`/`DisabilityCategory` enums. We still want parents picking from
// a fixed list of readable values, while never silently discarding whatever
// text already lives on an existing child's record. This module is the pure,
// testable normalization layer that makes that possible:
//
//   - `GRADE_LEVEL_OPTIONS` / `DISABILITY_CATEGORY_OPTIONS` are the canonical,
//     ordered, readable strings stored on submit.
//   - `normalizeGradeLevel` / `normalizeDisabilityCategory` map a stored (or
//     legacy) value to its canonical option using case-insensitive,
//     whitespace-tolerant matching — including a few safe aliases. A value
//     that matches nothing is returned unchanged (trimmed) rather than
//     dropped.
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

export interface ChildProfileOption {
  value: string;
  label: string;
}

/** Lowercases, trims, and collapses internal whitespace for alias lookups. */
function normalizeKey(value: string): string {
  return value.trim().toLowerCase().replace(/\s+/g, " ");
}

function ordinal(n: number): string {
  const rem100 = n % 100;
  if (rem100 >= 11 && rem100 <= 13) return `${n}th`;
  switch (n % 10) {
    case 1:
      return `${n}st`;
    case 2:
      return `${n}nd`;
    case 3:
      return `${n}rd`;
    default:
      return `${n}th`;
  }
}

// ---------------------------------------------------------------- Grade level

export const GRADE_LEVEL_OPTIONS: readonly string[] = [
  "Pre-K",
  "Kindergarten",
  ...Array.from({ length: 12 }, (_, i) => ordinal(i + 1)),
  "Ungraded",
];

function buildGradeAliases(): Map<string, string> {
  const aliases = new Map<string, string>();
  const add = (key: string, canonical: string) => {
    aliases.set(normalizeKey(key), canonical);
  };

  add("pk", "Pre-K");
  add("pre-k", "Pre-K");
  add("prek", "Pre-K");
  add("pre k", "Pre-K");
  add("pre kindergarten", "Pre-K");
  add("pre-kindergarten", "Pre-K");

  add("k", "Kindergarten");
  add("kinder", "Kindergarten");
  add("kindergarten", "Kindergarten");

  for (let n = 1; n <= 12; n++) {
    const canonical = ordinal(n);
    add(canonical, canonical); // "8th"
    add(`${n}`, canonical); // "8"
    add(`grade ${n}`, canonical); // "grade 8"
    add(`${canonical} grade`, canonical); // "8th grade"
    add(`g${n}`, canonical); // "g8"
    add(`grade ${canonical}`, canonical); // "grade 8th"
  }

  add("ungraded", "Ungraded");

  return aliases;
}

const GRADE_ALIASES = buildGradeAliases();

/**
 * Maps a stored/legacy grade-level string to its canonical
 * `GRADE_LEVEL_OPTIONS` entry. Case-insensitive and whitespace-tolerant. A
 * value that matches no known alias is returned trimmed, unchanged —
 * callers surface it as an extra selected option rather than lose it.
 */
export function normalizeGradeLevel(value: string | null | undefined): string {
  if (!value) return "";
  const trimmed = value.trim();
  if (!trimmed) return "";
  return GRADE_ALIASES.get(normalizeKey(trimmed)) ?? trimmed;
}

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

// The API's `DisabilityCategory.ToDisplay()` strings — what ChildLinkService
// writes into ChildProfile when a parent accepts a school link. Most differ
// from our labels only by case; "Visual Impairment" differs in wording.
export const SERVER_DISPLAY_LABELS: Readonly<
  Record<(typeof DISABILITY_CATEGORIES)[number], string>
> = {
  Autism: "Autism",
  DeafBlindness: "Deaf-Blindness",
  Deafness: "Deafness",
  DevelopmentalDelay: "Developmental Delay",
  EmotionalDisturbance: "Emotional Disturbance",
  HearingImpairment: "Hearing Impairment",
  IntellectualDisability: "Intellectual Disability",
  MultipleDisabilities: "Multiple Disabilities",
  OrthopedicImpairment: "Orthopedic Impairment",
  OtherHealthImpairment: "Other Health Impairment",
  SpecificLearningDisability: "Specific Learning Disability",
  SpeechOrLanguageImpairment: "Speech or Language Impairment",
  TraumaticBrainInjury: "Traumatic Brain Injury",
  VisualImpairment: "Visual Impairment",
  Other: "Other",
};

function buildDisabilityAliases(): Map<string, string> {
  const aliases = new Map<string, string>();
  const add = (key: string, canonicalLabel: string) => {
    aliases.set(normalizeKey(key), canonicalLabel);
  };

  for (const category of DISABILITY_CATEGORIES) {
    const label = DISABILITY_CATEGORY_LABELS[category];
    add(label, label); // readable label, any case
    add(category, label); // server enum code, e.g. "SpecificLearningDisability"
    add(SERVER_DISPLAY_LABELS[category], label); // what a school link writes
  }

  // A short, deliberately conservative list of abbreviations: each maps to
  // exactly one IDEA category with no plausible alternate reading among our
  // 15 options, so adding it can't misfile a legacy value.
  //   SLD -> Specific learning disability (standard special-ed abbreviation)
  //   OHI -> Other health impairment (standard special-ed abbreviation)
  //   TBI -> Traumatic brain injury (standard special-ed abbreviation)
  //   ASD -> Autism (IDEA's "Autism" category covers autism spectrum
  //          disorder; no other category here could plausibly be "ASD")
  add("SLD", DISABILITY_CATEGORY_LABELS.SpecificLearningDisability);
  add("OHI", DISABILITY_CATEGORY_LABELS.OtherHealthImpairment);
  add("TBI", DISABILITY_CATEGORY_LABELS.TraumaticBrainInjury);
  add("ASD", DISABILITY_CATEGORY_LABELS.Autism);

  return aliases;
}

const DISABILITY_ALIASES = buildDisabilityAliases();

/**
 * Maps a stored/legacy disability-category string — a readable label (any
 * case), a server enum code, or one of the safe abbreviations above — to its
 * canonical `DISABILITY_CATEGORY_OPTIONS` label. A value that matches
 * nothing is returned trimmed, unchanged.
 */
export function normalizeDisabilityCategory(
  value: string | null | undefined,
): string {
  if (!value) return "";
  const trimmed = value.trim();
  if (!trimmed) return "";
  return DISABILITY_ALIASES.get(normalizeKey(trimmed)) ?? trimmed;
}

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
