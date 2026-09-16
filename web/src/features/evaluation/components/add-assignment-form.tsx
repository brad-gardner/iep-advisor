import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { apiErrorMessage } from '@/lib/api-error';
import { getEligibleTeamStaff } from '@/features/educator/api/educator-api';
import type { EligibleStaff } from '@/features/educator/types';
import { addEvaluatorAssignment } from '../api/evaluation-api';
import type { EvaluatorAssignmentDto } from '../types';

interface AddAssignmentFormProps {
  studentId: number;
  onAdded: (assignment: EvaluatorAssignmentDto) => void;
}

/** Assign an evaluator: staff picker (the student's eligible team staff),
 *  domain, optional due date. */
export function AddAssignmentForm({ studentId, onAdded }: AddAssignmentFormProps) {
  const [staff, setStaff] = useState<EligibleStaff[] | null>(null);
  const [userId, setUserId] = useState('');
  const [domain, setDomain] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    getEligibleTeamStaff(studentId)
      .then((res) => {
        if (active) setStaff(res.success && res.data ? res.data : []);
      })
      .catch(() => {
        if (active) setStaff([]);
      });
    return () => {
      active = false;
    };
  }, [studentId]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!userId || !domain.trim()) {
      setError('Select a staff member and enter a domain.');
      return;
    }
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await addEvaluatorAssignment(studentId, {
        userId: Number(userId),
        domain: domain.trim(),
        dueDate: dueDate || undefined,
      });
      if (res.success && res.data) {
        onAdded(res.data);
        setUserId('');
        setDomain('');
        setDueDate('');
      } else {
        setError(res.message ?? 'Could not add the assignment.');
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not add the assignment.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3" data-testid="add-assignment-form">
      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}
      <div className="grid gap-3 sm:grid-cols-3">
        <Select
          label="Evaluator *"
          value={userId}
          onChange={(e) => setUserId(e.target.value)}
          disabled={staff === null}
          data-testid="add-assignment-staff"
        >
          <option value="">{staff === null ? 'Loading…' : 'Select…'}</option>
          {(staff ?? []).map((s) => (
            <option key={s.userId} value={s.userId}>
              {`${s.firstName} ${s.lastName}`.trim() || s.email}
            </option>
          ))}
        </Select>
        <Input
          label="Domain *"
          placeholder="e.g. Speech-language"
          maxLength={100}
          value={domain}
          onChange={(e) => setDomain(e.target.value)}
          data-testid="add-assignment-domain"
        />
        <Input
          label="Due date"
          type="date"
          value={dueDate}
          onChange={(e) => setDueDate(e.target.value)}
          data-testid="add-assignment-due-date"
        />
      </div>
      <Button type="submit" size="sm" loading={isSubmitting} data-testid="add-assignment-submit">
        Add evaluator
      </Button>
    </form>
  );
}
