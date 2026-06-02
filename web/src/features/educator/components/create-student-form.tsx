import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import type { CreateSchoolStudentRequest } from '../types';

interface CreateStudentFormProps {
  onSubmit: (data: CreateSchoolStudentRequest) => Promise<{ success: boolean; error?: string }>;
}

export function CreateStudentForm({ onSubmit }: CreateStudentFormProps) {
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [gradeLevel, setGradeLevel] = useState('');
  const [disabilityCategory, setDisabilityCategory] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);

    const result = await onSubmit({
      firstName: firstName.trim(),
      lastName: lastName.trim() || undefined,
      gradeLevel: gradeLevel.trim() || undefined,
      disabilityCategory: disabilityCategory.trim() || undefined,
    });

    if (result.success) {
      setFirstName('');
      setLastName('');
      setGradeLevel('');
      setDisabilityCategory('');
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
