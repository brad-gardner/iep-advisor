// Pure normalization for the parent-side child profile free-text fields
// (grade level / disability category) — lives in `lib/` (not
// `features/children/`) specifically so the lib-level label helpers
// (`grade-level-label.ts`, `disability-category-label.ts`) can import it
// without a lib→feature→lib import cycle: those helpers translate an
// already-normalized value, and `features/children/lib/child-profile-options.ts`
// (the dropdown-options module) imports them back for its own option labels.
// See that module's comment for the full picture of how normalization,
// canonical options, and display labels fit together; it re-exports
// `normalizeGradeLevel`/`normalizeDisabilityCategory`/`SERVER_DISPLAY_LABELS`
// from here for backward compatibility.
import {
  DISABILITY_CATEGORIES,
  DISABILITY_CATEGORY_LABELS,
} from "@/features/educator/types";

/** Lowercases, trims, and collapses internal whitespace for alias lookups. */
function normalizeKey(value: string): string {
  return value.trim().toLowerCase().replace(/\s+/g, " ");
}

export function ordinal(n: number): string {
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

// ------------------------------------------------------------ Disability category

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
