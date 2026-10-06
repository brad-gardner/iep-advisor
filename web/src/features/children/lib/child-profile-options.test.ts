import { describe, it, expect } from "vitest";
import { DISABILITY_CATEGORY_LABELS } from "@/features/educator/types";
import {
  GRADE_LEVEL_OPTIONS,
  DISABILITY_CATEGORY_OPTIONS,
  normalizeGradeLevel,
  normalizeDisabilityCategory,
  buildGradeLevelOptions,
  buildDisabilityCategoryOptions,
  SERVER_DISPLAY_LABELS,
} from "./child-profile-options";

describe("GRADE_LEVEL_OPTIONS", () => {
  it("lists grades in order: Pre-K, Kindergarten, 1st..12th, Ungraded", () => {
    expect(GRADE_LEVEL_OPTIONS).toEqual([
      "Pre-K",
      "Kindergarten",
      "1st",
      "2nd",
      "3rd",
      "4th",
      "5th",
      "6th",
      "7th",
      "8th",
      "9th",
      "10th",
      "11th",
      "12th",
      "Ungraded",
    ]);
  });
});

describe("DISABILITY_CATEGORY_OPTIONS", () => {
  it("lists the 14 IDEA categories + Other, in DISABILITY_CATEGORIES order", () => {
    expect(DISABILITY_CATEGORY_OPTIONS).toEqual([
      "Autism",
      "Deaf-blindness",
      "Deafness",
      "Developmental delay",
      "Emotional disturbance",
      "Hearing impairment",
      "Intellectual disability",
      "Multiple disabilities",
      "Orthopedic impairment",
      "Other health impairment",
      "Specific learning disability",
      "Speech or language impairment",
      "Traumatic brain injury",
      "Visual impairment (including blindness)",
      "Other",
    ]);
    expect(DISABILITY_CATEGORY_OPTIONS).toHaveLength(15);
  });
});

describe("normalizeGradeLevel", () => {
  it.each([
    ["", ""],
    [undefined, ""],
    [null, ""],
    ["  ", ""],
    ["8", "8th"],
    ["8th", "8th"],
    ["Grade 8", "8th"],
    ["8th grade", "8th"],
    ["G8", "8th"],
    ["grade 8th", "8th"],
    ["  8th  ", "8th"],
    ["K", "Kindergarten"],
    ["Kinder", "Kindergarten"],
    ["Kindergarten", "Kindergarten"],
    ["kindergarten", "Kindergarten"],
    ["PK", "Pre-K"],
    ["Pre-K", "Pre-K"],
    ["PreK", "Pre-K"],
    ["Pre-Kindergarten", "Pre-K"],
    ["1", "1st"],
    ["2", "2nd"],
    ["3", "3rd"],
    ["11", "11th"],
    ["12", "12th"],
    ["Ungraded", "Ungraded"],
    ["UNGRADED", "Ungraded"],
  ] as const)("maps %s -> %s", (input, expected) => {
    expect(normalizeGradeLevel(input)).toBe(expected);
  });

  it("preserves an unmatched legacy value, trimmed but otherwise unchanged", () => {
    expect(normalizeGradeLevel("  8th grade (honors track)  ")).toBe(
      "8th grade (honors track)",
    );
  });
});

describe("normalizeDisabilityCategory", () => {
  it.each([
    ["", ""],
    [undefined, ""],
    [null, ""],
    ["autism", "Autism"],
    ["AUTISM", "Autism"],
    ["Autism", "Autism"],
    ["SpecificLearningDisability", "Specific learning disability"],
    ["specificlearningdisability", "Specific learning disability"],
    ["specific learning disability", "Specific learning disability"],
    ["SLD", "Specific learning disability"],
    ["OHI", "Other health impairment"],
    ["TBI", "Traumatic brain injury"],
    ["ASD", "Autism"],
    ["OtherHealthImpairment", "Other health impairment"],
    ["  Autism  ", "Autism"],
  ] as const)("maps %s -> %s", (input, expected) => {
    expect(normalizeDisabilityCategory(input)).toBe(expected);
  });

  it("preserves an unmatched legacy value, trimmed but otherwise unchanged", () => {
    expect(normalizeDisabilityCategory("  Speech/Language (mild)  ")).toBe(
      "Speech/Language (mild)",
    );
  });
});

describe("SERVER_DISPLAY_LABELS", () => {
  it.each(Object.entries(SERVER_DISPLAY_LABELS))(
    "%s's server display string %s resolves to its canonical option",
    (category, display) => {
      expect(normalizeDisabilityCategory(display)).toBe(
        DISABILITY_CATEGORY_LABELS[category as keyof typeof DISABILITY_CATEGORY_LABELS],
      );
    },
  );
});

describe("buildGradeLevelOptions", () => {
  it("starts with Not set, then every canonical grade, with no extra entry for a matched value", () => {
    const options = buildGradeLevelOptions("8th");
    expect(options[0]).toEqual({ value: "", label: "Not set" });
    expect(options.map((o) => o.value)).toEqual(["", ...GRADE_LEVEL_OPTIONS]);
  });

  it("appends the raw legacy value as an extra selected option when unmatched", () => {
    const options = buildGradeLevelOptions("8th grade (honors track)");
    const extra = options[options.length - 1];
    expect(extra).toEqual({
      value: "8th grade (honors track)",
      label: "8th grade (honors track) (current value)",
    });
  });

  it("adds no extra option for Not set / undefined", () => {
    expect(buildGradeLevelOptions(undefined)).toHaveLength(
      GRADE_LEVEL_OPTIONS.length + 1,
    );
  });
});

describe("buildDisabilityCategoryOptions", () => {
  it("starts with Not set, then every canonical label, with no extra entry for a matched value", () => {
    const options = buildDisabilityCategoryOptions("Autism");
    expect(options[0]).toEqual({ value: "", label: "Not set" });
    expect(options.map((o) => o.value)).toEqual([
      "",
      ...DISABILITY_CATEGORY_OPTIONS,
    ]);
  });

  it("appends the raw legacy value as an extra selected option when unmatched", () => {
    const options = buildDisabilityCategoryOptions("Speech/Language (mild)");
    const extra = options[options.length - 1];
    expect(extra).toEqual({
      value: "Speech/Language (mild)",
      label: "Speech/Language (mild) (current value)",
    });
  });
});
