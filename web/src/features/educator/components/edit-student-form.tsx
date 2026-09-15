import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import type { SchoolStudent, UpdateSchoolStudentRequest } from '../types';
import { fieldsFrom, toUpdateRequest, type EditStudentFields } from '../lib/student-edit-fields';
import { DisabilityCategorySelect, GradeLevelSelect } from './student-enum-selects';

interface EditStudentFormProps {
  student: SchoolStudent;
  onSubmit: (data: UpdateSchoolStudentRequest) => Promise<{ success: boolean; error?: string }>;
  onCancel: () => void;
}

export function EditStudentForm({ student, onSubmit, onCancel }: EditStudentFormProps) {
  const [fields, setFields] = useState<EditStudentFields>(() => fieldsFrom(student));
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const set = (patch: Partial<EditStudentFields>) => setFields((prev) => ({ ...prev, ...patch }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!fields.firstName.trim()) {
      setError('First name is required');
      return;
    }
    setIsSubmitting(true);
    const result = await onSubmit(toUpdateRequest(fields));
    if (!result.success) setError(result.error ?? 'Could not save the student');
    setIsSubmitting(false);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-5" data-testid="edit-student-form">
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      <fieldset className="space-y-4">
        <legend className="mb-1 text-xs font-medium uppercase tracking-wide text-brand-slate-400">
          Identity
        </legend>
        <Input
          id="edit-student-first-name"
          label="First name *"
          required
          maxLength={100}
          value={fields.firstName}
          onChange={(e) => set({ firstName: e.target.value })}
          data-testid="edit-student-first-name"
        />
        <Input
          id="edit-student-last-name"
          label="Last name"
          maxLength={100}
          value={fields.lastName}
          onChange={(e) => set({ lastName: e.target.value })}
          data-testid="edit-student-last-name"
        />
        <Input
          id="edit-student-external-id"
          label="Student ID"
          maxLength={64}
          value={fields.externalStudentId}
          onChange={(e) => set({ externalStudentId: e.target.value })}
          data-testid="edit-student-external-id"
        />
        <Input
          id="edit-student-dob"
          label="Date of birth"
          type="date"
          value={fields.dateOfBirth}
          onChange={(e) => set({ dateOfBirth: e.target.value })}
          data-testid="edit-student-dob"
        />
        <Input
          id="edit-student-state"
          label="State"
          maxLength={2}
          placeholder="OH"
          value={fields.stateCode}
          onChange={(e) => set({ stateCode: e.target.value })}
          data-testid="edit-student-state"
        />
      </fieldset>

      <fieldset className="space-y-4">
        <legend className="mb-1 text-xs font-medium uppercase tracking-wide text-brand-slate-400">
          Placement
        </legend>
        <GradeLevelSelect
          id="edit-student-grade"
          value={fields.gradeLevel}
          onChange={(gradeLevel) => set({ gradeLevel })}
          data-testid="edit-student-grade"
        />
        <DisabilityCategorySelect
          id="edit-student-disability"
          value={fields.disabilityCategory}
          onChange={(disabilityCategory) => set({ disabilityCategory })}
          data-testid="edit-student-disability"
        />
        <Input
          id="edit-student-home-language"
          label="Home language"
          maxLength={32}
          placeholder="en"
          value={fields.homeLanguage}
          onChange={(e) => set({ homeLanguage: e.target.value })}
          data-testid="edit-student-home-language"
        />
      </fieldset>

      <fieldset className="space-y-4">
        <legend className="mb-1 text-xs font-medium uppercase tracking-wide text-brand-slate-400">
          Timeline
        </legend>
        <Input
          id="edit-student-iep-date"
          label="IEP date"
          type="date"
          value={fields.iepDate}
          onChange={(e) => set({ iepDate: e.target.value })}
          data-testid="edit-student-iep-date"
        />
        <Input
          id="edit-student-annual-review"
          label="Annual review due"
          type="date"
          value={fields.annualReviewDueDate}
          onChange={(e) => set({ annualReviewDueDate: e.target.value })}
          data-testid="edit-student-annual-review"
        />
        <Input
          id="edit-student-etr-date"
          label="ETR date"
          type="date"
          value={fields.etrDate}
          onChange={(e) => set({ etrDate: e.target.value })}
          data-testid="edit-student-etr-date"
        />
        <Input
          id="edit-student-reevaluation"
          label="Reevaluation due"
          type="date"
          value={fields.reevaluationDueDate}
          onChange={(e) => set({ reevaluationDueDate: e.target.value })}
          data-testid="edit-student-reevaluation"
        />
      </fieldset>

      <div className="flex items-center justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
        <Button type="submit" loading={isSubmitting} data-testid="edit-student-submit">
          Save changes
        </Button>
      </div>
    </form>
  );
}
