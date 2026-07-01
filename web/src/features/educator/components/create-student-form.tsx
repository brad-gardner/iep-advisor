import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Input, Select } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import type { DistrictSchool } from '@/features/district-admin/types';
import type { CreateSchoolStudentRequest } from '../types';

interface CreateStudentFormProps {
  onSubmit: (data: CreateSchoolStudentRequest) => Promise<{ success: boolean; error?: string }>;
  // When provided (DistrictAdmin callers), a required school picker is shown and
  // its value is sent as schoolId. SchoolAdmin/Teacher callers omit this.
  schools?: DistrictSchool[];
}

export function CreateStudentForm({ onSubmit, schools }: CreateStudentFormProps) {
  const requiresSchool = schools !== undefined;
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [gradeLevel, setGradeLevel] = useState('');
  const [disabilityCategory, setDisabilityCategory] = useState('');
  const [schoolId, setSchoolId] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (requiresSchool && !schoolId) {
      setError('Select a school for this student');
      return;
    }

    setIsSubmitting(true);

    const result = await onSubmit({
      firstName: firstName.trim(),
      lastName: lastName.trim() || undefined,
      gradeLevel: gradeLevel.trim() || undefined,
      disabilityCategory: disabilityCategory.trim() || undefined,
      schoolId: requiresSchool ? Number(schoolId) : undefined,
    });

    if (result.success) {
      setFirstName('');
      setLastName('');
      setGradeLevel('');
      setDisabilityCategory('');
      setSchoolId('');
    } else {
      setError(result.error ?? 'Something went wrong');
    }

    setIsSubmitting(false);
  };

  return (
    <Card className="max-w-lg">
      <h2 className="font-serif text-lg mb-4">Add a student</h2>
      <form onSubmit={handleSubmit} className="space-y-4" data-testid="create-student-form">
        {error && <Notice variant="error" title={error} />}

        {requiresSchool && (
          <Select
            label="School *"
            required
            value={schoolId}
            onChange={(e) => setSchoolId(e.target.value)}
            data-testid="educator-student-create-school"
          >
            <option value="">Select a school</option>
            {schools!.map((school) => (
              <option key={school.id} value={school.id}>
                {school.name}
              </option>
            ))}
          </Select>
        )}

        <Input
          label="First Name *"
          required
          value={firstName}
          onChange={(e) => setFirstName(e.target.value)}
          maxLength={100}
          data-testid="student-first-name"
        />

        <Input
          label="Last Name"
          value={lastName}
          onChange={(e) => setLastName(e.target.value)}
          maxLength={100}
          data-testid="student-last-name"
        />

        <Input
          label="Grade Level"
          placeholder="e.g. 3rd, 7th, 10th"
          value={gradeLevel}
          onChange={(e) => setGradeLevel(e.target.value)}
          maxLength={50}
          data-testid="student-grade-level"
        />

        <Input
          label="Disability Category"
          placeholder="e.g. Autism, SLD"
          value={disabilityCategory}
          onChange={(e) => setDisabilityCategory(e.target.value)}
          maxLength={100}
          data-testid="student-disability-category"
        />

        <Button
          type="submit"
          disabled={isSubmitting}
          className="w-full"
          data-testid="create-student-submit"
        >
          {isSubmitting ? 'Adding...' : 'Add Student'}
        </Button>
      </form>
    </Card>
  );
}
