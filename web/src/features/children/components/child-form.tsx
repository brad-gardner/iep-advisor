import { useState } from 'react';
import type { CreateChildProfileRequest } from '@/types/api';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';

interface ChildFormProps {
  initialValues?: Partial<CreateChildProfileRequest>;
  onSubmit: (data: CreateChildProfileRequest) => Promise<{ success: boolean; error?: string }>;
  submitLabel: string;
}

export function ChildForm({ initialValues, onSubmit, submitLabel }: ChildFormProps) {
  const [firstName, setFirstName] = useState(initialValues?.firstName ?? '');
  const [lastName, setLastName] = useState(initialValues?.lastName ?? '');
  const [dateOfBirth, setDateOfBirth] = useState(initialValues?.dateOfBirth ?? '');
  const [gradeLevel, setGradeLevel] = useState(initialValues?.gradeLevel ?? '');
  const [disabilityCategory, setDisabilityCategory] = useState(
    initialValues?.disabilityCategory ?? ''
  );
  const [schoolDistrict, setSchoolDistrict] = useState(initialValues?.schoolDistrict ?? '');
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
      setError(result.error ?? 'Something went wrong');
    }

    setIsSubmitting(false);
  };

  return (
    <Card className="max-w-lg">
      <form onSubmit={handleSubmit} className="space-y-4">
        {error && <Notice variant="error" title={error} />}

        <Input
          label="First Name *"
          required
          value={firstName}
          onChange={(e) => setFirstName(e.target.value)}
          maxLength={100}
        />

        <Input
          label="Last Name"
          value={lastName}
          onChange={(e) => setLastName(e.target.value)}
          maxLength={100}
        />

        <Input
          label="Date of Birth"
          type="date"
          value={dateOfBirth}
          onChange={(e) => setDateOfBirth(e.target.value)}
        />

        <Input
          label="Grade Level"
          placeholder="e.g. 3rd, 7th, 10th"
          value={gradeLevel}
          onChange={(e) => setGradeLevel(e.target.value)}
          maxLength={20}
        />

        <Input
          label="Disability Category"
          placeholder="e.g. Autism, SLD, Speech/Language"
          value={disabilityCategory}
          onChange={(e) => setDisabilityCategory(e.target.value)}
          maxLength={100}
        />

        <Input
          label="School District"
          value={schoolDistrict}
          onChange={(e) => setSchoolDistrict(e.target.value)}
          maxLength={200}
        />

        <Button type="submit" disabled={isSubmitting} className="w-full">
          {isSubmitting ? 'Saving...' : submitLabel}
        </Button>
      </form>
    </Card>
  );
}
