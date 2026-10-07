import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Card } from "@/components/ui/card";
import { Input, Select } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Notice } from "@/components/ui/notice";
import type { DistrictSchool } from "@/features/district-admin/types";
import type {
  CreateSchoolStudentRequest,
  DisabilityCategory,
  GradeLevel,
} from "../types";
import { DisabilityCategorySelect, GradeLevelSelect } from "./student-enum-selects";

interface CreateStudentFormProps {
  onSubmit: (
    data: CreateSchoolStudentRequest,
  ) => Promise<{ success: boolean; error?: string }>;
  // When provided (DistrictAdmin callers), a required school picker is shown and
  // its value is sent as schoolId. SchoolAdmin/Teacher callers omit this.
  schools?: DistrictSchool[];
  /** When hosted inside a Modal/Drawer, drop the self-`Card` + heading. */
  embedded?: boolean;
}

export function CreateStudentForm({
  onSubmit,
  schools,
  embedded = false,
}: CreateStudentFormProps) {
  const { t } = useTranslation('educator');
  const requiresSchool = schools !== undefined;
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [externalStudentId, setExternalStudentId] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [gradeLevel, setGradeLevel] = useState("");
  const [disabilityCategory, setDisabilityCategory] = useState("");
  const [schoolId, setSchoolId] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (requiresSchool && !schoolId) {
      setError(t('createStudentForm.selectSchoolRequired'));
      return;
    }

    setIsSubmitting(true);

    const result = await onSubmit({
      firstName: firstName.trim(),
      lastName: lastName.trim() || undefined,
      externalStudentId: externalStudentId.trim() || undefined,
      dateOfBirth: dateOfBirth || undefined,
      gradeLevel: (gradeLevel as GradeLevel) || undefined,
      disabilityCategory: (disabilityCategory as DisabilityCategory) || undefined,
      schoolId: requiresSchool ? Number(schoolId) : undefined,
    });

    if (result.success) {
      setFirstName("");
      setLastName("");
      setExternalStudentId("");
      setDateOfBirth("");
      setGradeLevel("");
      setDisabilityCategory("");
      setSchoolId("");
    } else {
      setError(result.error ?? t('createStudentForm.genericError'));
    }

    setIsSubmitting(false);
  };

  const form = (
    <form
      onSubmit={handleSubmit}
      className="space-y-4"
      data-testid="create-student-form"
    >
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      {requiresSchool && (
        <Select
          label={t('createStudentForm.schoolLabel')}
          required
          value={schoolId}
          onChange={(e) => setSchoolId(e.target.value)}
          data-testid="educator-student-create-school"
        >
          <option value="">{t('createStudentForm.selectSchool')}</option>
          {schools!.map((school) => (
            <option key={school.id} value={school.id}>
              {school.name}
            </option>
          ))}
        </Select>
      )}

      <Input
        label={t('createStudentForm.firstNameLabel')}
        required
        value={firstName}
        onChange={(e) => setFirstName(e.target.value)}
        maxLength={100}
        data-testid="student-first-name"
      />

      <Input
        label={t('createStudentForm.lastNameLabel')}
        value={lastName}
        onChange={(e) => setLastName(e.target.value)}
        maxLength={100}
        data-testid="student-last-name"
      />

      <Input
        label={t('createStudentForm.studentIdLabel')}
        placeholder={t('createStudentForm.studentIdPlaceholder')}
        value={externalStudentId}
        onChange={(e) => setExternalStudentId(e.target.value)}
        maxLength={64}
        data-testid="student-external-id"
      />

      <Input
        label={t('createStudentForm.dobLabel')}
        type="date"
        value={dateOfBirth}
        onChange={(e) => setDateOfBirth(e.target.value)}
        data-testid="student-date-of-birth"
      />

      <GradeLevelSelect
        id="student-grade-level"
        value={gradeLevel}
        onChange={setGradeLevel}
        data-testid="student-grade-level"
      />

      <DisabilityCategorySelect
        id="student-disability-category"
        value={disabilityCategory}
        onChange={setDisabilityCategory}
        data-testid="student-disability-category"
      />

      <Button
        type="submit"
        disabled={isSubmitting}
        className="w-full"
        data-testid="create-student-submit"
      >
        {isSubmitting ? t('createStudentForm.submitting') : t('createStudentForm.submit')}
      </Button>
    </form>
  );

  if (embedded) return form;
  return (
    <Card className="max-w-lg">
      <h2 className="font-serif text-lg mb-4">{t('studentsPage.addStudentModalTitle')}</h2>
      {form}
    </Card>
  );
}
