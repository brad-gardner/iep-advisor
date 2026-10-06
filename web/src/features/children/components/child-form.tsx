import { useMemo, useState } from "react";
import type { CreateChildProfileRequest } from "@/types/api";
import { Input, Select } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Notice } from "@/components/ui/notice";
import {
  buildDisabilityCategoryOptions,
  buildGradeLevelOptions,
  normalizeDisabilityCategory,
  normalizeGradeLevel,
} from "../lib/child-profile-options";

interface ChildFormProps {
  initialValues?: Partial<CreateChildProfileRequest>;
  onSubmit: (
    data: CreateChildProfileRequest,
  ) => Promise<{ success: boolean; error?: string }>;
  submitLabel: string;
  /** When hosted inside a Modal/Drawer, drop the self-`Card` wrapper. */
  embedded?: boolean;
}

export function ChildForm({
  initialValues,
  onSubmit,
  submitLabel,
  embedded = false,
}: ChildFormProps) {
  const [firstName, setFirstName] = useState(initialValues?.firstName ?? "");
  const [lastName, setLastName] = useState(initialValues?.lastName ?? "");
  const [dateOfBirth, setDateOfBirth] = useState(
    initialValues?.dateOfBirth ?? "",
  );
  const [gradeLevel, setGradeLevel] = useState(
    normalizeGradeLevel(initialValues?.gradeLevel),
  );
  const [disabilityCategory, setDisabilityCategory] = useState(
    normalizeDisabilityCategory(initialValues?.disabilityCategory),
  );
  // Computed once from the value this form mounted with — Modal unmounts the
  // form while closed, so a fresh edit open recomputes against the latest
  // record. Keeps a legacy/unmatched value selectable (and thus preserved on
  // submit) without rebuilding the list on every keystroke.
  const gradeLevelOptions = useMemo(
    () => buildGradeLevelOptions(initialValues?.gradeLevel),
    [initialValues?.gradeLevel],
  );
  const disabilityCategoryOptions = useMemo(
    () => buildDisabilityCategoryOptions(initialValues?.disabilityCategory),
    [initialValues?.disabilityCategory],
  );
  const [schoolDistrict, setSchoolDistrict] = useState(
    initialValues?.schoolDistrict ?? "",
  );

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);

    const result = await onSubmit({
      firstName: firstName.trim(),
      lastName: lastName.trim() || undefined,
      dateOfBirth: dateOfBirth || undefined,
      gradeLevel: gradeLevel.trim() || undefined,
      disabilityCategory: disabilityCategory.trim() || undefined,
      schoolDistrict: schoolDistrict.trim() || undefined,
    });

    if (!result.success) {
      setError(result.error ?? "Something went wrong");
    }

    setIsSubmitting(false);
  };

  const form = (
    <form
      onSubmit={handleSubmit}
      className="space-y-4"
      data-testid="child-form"
    >
      {error && <Notice variant="error" title={error} />}

      <Input
        label="First Name *"
        required
        value={firstName}
        onChange={(e) => setFirstName(e.target.value)}
        maxLength={100}
        data-testid="child-first-name"
      />

      <Input
        label="Last Name"
        value={lastName}
        onChange={(e) => setLastName(e.target.value)}
        maxLength={100}
        data-testid="child-last-name"
      />

      <Input
        label="Date of Birth"
        type="date"
        value={dateOfBirth}
        onChange={(e) => setDateOfBirth(e.target.value)}
        data-testid="child-date-of-birth"
      />

      <Select
        label="Grade Level"
        value={gradeLevel}
        onChange={(e) => setGradeLevel(e.target.value)}
        data-testid="child-grade-level"
      >
        {gradeLevelOptions.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </Select>

      <Select
        label="Disability Category"
        value={disabilityCategory}
        onChange={(e) => setDisabilityCategory(e.target.value)}
        data-testid="child-disability-category"
      >
        {disabilityCategoryOptions.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </Select>

      <Input
        label="School District"
        value={schoolDistrict}
        onChange={(e) => setSchoolDistrict(e.target.value)}
        maxLength={200}
        data-testid="child-school-district"
      />

      <Button
        type="submit"
        disabled={isSubmitting}
        className="w-full"
        data-testid="child-form-submit"
      >
        {isSubmitting ? "Saving..." : submitLabel}
      </Button>
    </form>
  );

  if (embedded) return form;
  return <Card className="max-w-lg">{form}</Card>;
}
