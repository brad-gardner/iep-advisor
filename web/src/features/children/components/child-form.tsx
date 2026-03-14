import { useState } from 'react';
import type { CreateChildProfileRequest } from '@/types/api';

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
      firstName,
      lastName: lastName || undefined,
      dateOfBirth: dateOfBirth || undefined,
      gradeLevel: gradeLevel || undefined,
      disabilityCategory: disabilityCategory || undefined,
      schoolDistrict: schoolDistrict || undefined,
    });

    if (!result.success) {
      setError(result.error ?? 'Something went wrong');
    }

    setIsSubmitting(false);
  };

  return (
    <form onSubmit={handleSubmit} className="bg-white rounded-lg p-6 max-w-lg space-y-4 shadow-sm border border-gray-200">
      {error && (
        <div className="p-3 rounded text-sm bg-red-50 text-red-600">{error}</div>
      )}

      <div>
        <label htmlFor="firstName" className="block text-sm text-gray-500 mb-1">
          First Name *
        </label>
        <input
          id="firstName"
          type="text"
          required
          value={firstName}
          onChange={(e) => setFirstName(e.target.value)}
          className="w-full px-3 py-2 bg-white rounded text-gray-900 border border-gray-300 focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div>
        <label htmlFor="lastName" className="block text-sm text-gray-500 mb-1">
          Last Name
        </label>
        <input
          id="lastName"
          type="text"
          value={lastName}
          onChange={(e) => setLastName(e.target.value)}
          className="w-full px-3 py-2 bg-white rounded text-gray-900 border border-gray-300 focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div>
        <label htmlFor="dateOfBirth" className="block text-sm text-gray-500 mb-1">
          Date of Birth
        </label>
        <input
          id="dateOfBirth"
          type="date"
          value={dateOfBirth}
          onChange={(e) => setDateOfBirth(e.target.value)}
          className="w-full px-3 py-2 bg-white rounded text-gray-900 border border-gray-300 focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div>
        <label htmlFor="gradeLevel" className="block text-sm text-gray-500 mb-1">
          Grade Level
        </label>
        <input
          id="gradeLevel"
          type="text"
          placeholder="e.g. 3rd, 7th, 10th"
          value={gradeLevel}
          onChange={(e) => setGradeLevel(e.target.value)}
          className="w-full px-3 py-2 bg-white rounded text-gray-900 placeholder-gray-400 border border-gray-300 focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div>
        <label htmlFor="disabilityCategory" className="block text-sm text-gray-500 mb-1">
          Disability Category
        </label>
        <input
          id="disabilityCategory"
          type="text"
          placeholder="e.g. Autism, SLD, Speech/Language"
          value={disabilityCategory}
          onChange={(e) => setDisabilityCategory(e.target.value)}
          className="w-full px-3 py-2 bg-white rounded text-gray-900 placeholder-gray-400 border border-gray-300 focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div>
        <label htmlFor="schoolDistrict" className="block text-sm text-gray-500 mb-1">
          School District
        </label>
        <input
          id="schoolDistrict"
          type="text"
          value={schoolDistrict}
          onChange={(e) => setSchoolDistrict(e.target.value)}
          className="w-full px-3 py-2 bg-white rounded text-gray-900 border border-gray-300 focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
        className="w-full py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 rounded font-medium text-white transition-colors"
      >
        {isSubmitting ? 'Saving...' : submitLabel}
      </button>
    </form>
  );
}
