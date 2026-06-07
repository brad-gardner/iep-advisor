import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { SchoolForm } from './school-form';
import type { DistrictSchool, SaveSchoolRequest } from '../types';

interface SchoolRowProps {
  school: DistrictSchool;
  onUpdate: (
    schoolId: number,
    data: SaveSchoolRequest
  ) => Promise<{ success: boolean; error?: string }>;
  onDeactivate: (schoolId: number) => Promise<{ success: boolean; error?: string }>;
}

export function SchoolRow({ school, onUpdate, onDeactivate }: SchoolRowProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [isConfirmingDeactivate, setIsConfirmingDeactivate] = useState(false);
  const [isDeactivating, setIsDeactivating] = useState(false);
  const [deactivateError, setDeactivateError] = useState<string | null>(null);

  const handleUpdate = async (data: SaveSchoolRequest) => {
    const result = await onUpdate(school.id, data);
    if (result.success) {
      setIsEditing(false);
    }
    return result;
  };

  const handleDeactivate = async () => {
    setIsDeactivating(true);
    setDeactivateError(null);
    const result = await onDeactivate(school.id);
    if (!result.success) {
      setDeactivateError(result.error ?? 'Could not deactivate this school');
      setIsConfirmingDeactivate(false);
    }
    setIsDeactivating(false);
  };

  if (isEditing) {
    return (
      <li>
        <Card data-testid={`district-school-${school.id}`}>
          <SchoolForm
            mode="edit"
            initialName={school.name}
            initialStateCode={school.stateCode ?? ''}
            submitLabel="Save changes"
            onSubmit={handleUpdate}
            onCancel={() => setIsEditing(false)}
            testIdPrefix={`district-school-edit-form-${school.id}`}
          />
        </Card>
      </li>
    );
  }

  return (
    <li>
      <Card
        className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"
        data-testid={`district-school-${school.id}`}
      >
        <div className="space-y-1">
          <div className="flex items-center gap-2">
            <span className="text-brand-slate-800 font-medium">{school.name}</span>
            {school.stateCode && (
              <span className="text-sm text-brand-slate-500">{school.stateCode}</span>
            )}
          </div>
          <p className="text-xs text-brand-slate-400">
            {school.activeStudentCount} student{school.activeStudentCount === 1 ? '' : 's'}
            {' · '}
            {school.activeStaffCount} staff
          </p>
          {deactivateError && (
            <div className="pt-2">
              <Notice variant="error" title={deactivateError} />
            </div>
          )}
        </div>

        {isConfirmingDeactivate ? (
          <div className="flex shrink-0 items-center gap-2">
            <span className="text-sm text-brand-slate-600">Deactivate?</span>
            <Button
              variant="danger"
              onClick={handleDeactivate}
              disabled={isDeactivating}
              data-testid={`district-school-deactivate-confirm-${school.id}`}
            >
              {isDeactivating ? 'Deactivating...' : 'Confirm'}
            </Button>
            <Button
              variant="ghost"
              onClick={() => setIsConfirmingDeactivate(false)}
              disabled={isDeactivating}
              data-testid={`district-school-deactivate-cancel-${school.id}`}
            >
              Cancel
            </Button>
          </div>
        ) : (
          <div className="flex shrink-0 items-center gap-2">
            <Button
              variant="secondary"
              onClick={() => {
                setDeactivateError(null);
                setIsEditing(true);
              }}
              data-testid={`district-school-edit-${school.id}`}
            >
              Edit
            </Button>
            <Button
              variant="danger"
              onClick={() => {
                setDeactivateError(null);
                setIsConfirmingDeactivate(true);
              }}
              data-testid={`district-school-deactivate-${school.id}`}
            >
              Deactivate
            </Button>
          </div>
        )}
      </Card>
    </li>
  );
}
