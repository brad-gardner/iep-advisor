import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Notice } from '@/components/ui/notice';
import type { StaffMember } from '../types';

interface StaffRowProps {
  member: StaffMember;
  onDeactivate: (staffProfileId: number) => Promise<{ success: boolean; error?: string }>;
  onReactivate: (staffProfileId: number) => Promise<{ success: boolean; error?: string }>;
}

export function StaffRow({ member, onDeactivate, onReactivate }: StaffRowProps) {
  const [isConfirming, setIsConfirming] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fullName = `${member.firstName} ${member.lastName}`.trim() || member.email;

  const handleDeactivate = async () => {
    setIsSubmitting(true);
    setError(null);
    const result = await onDeactivate(member.staffProfileId);
    if (!result.success) {
      // The backend returns an explicit message for the last-DistrictAdmin
      // guard — surface it verbatim.
      setError(result.error ?? 'Could not deactivate this staff member');
    }
    setIsConfirming(false);
    setIsSubmitting(false);
  };

  const handleReactivate = async () => {
    setIsSubmitting(true);
    setError(null);
    const result = await onReactivate(member.staffProfileId);
    if (!result.success) {
      setError(result.error ?? 'Could not reactivate this staff member');
    }
    setIsSubmitting(false);
  };

  return (
    <li>
      <Card
        className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"
        data-testid={`district-staff-${member.staffProfileId}`}
      >
        <div className="space-y-1">
          <div className="flex items-center gap-2">
            <span className="text-brand-slate-800 font-medium">{fullName}</span>
            <Badge variant={member.isActive ? 'success' : 'neutral'}>
              {member.isActive ? 'Active' : 'Inactive'}
            </Badge>
          </div>
          <p className="text-sm text-brand-slate-500">{member.email}</p>
          <p className="text-xs text-brand-slate-400">
            {member.orgRoleName}
            {member.schoolName ? ` · ${member.schoolName}` : ' · District-wide'}
          </p>
          {error && (
            <div className="pt-2">
              <Notice variant="error" title={error} />
            </div>
          )}
        </div>

        <div className="flex shrink-0 items-center gap-2">
          {member.isActive ? (
            isConfirming ? (
              <>
                <span className="text-sm text-brand-slate-600">Deactivate?</span>
                <Button
                  variant="danger"
                  onClick={handleDeactivate}
                  disabled={isSubmitting}
                  data-testid={`district-staff-deactivate-confirm-${member.staffProfileId}`}
                >
                  {isSubmitting ? 'Deactivating...' : 'Confirm'}
                </Button>
                <Button
                  variant="ghost"
                  onClick={() => setIsConfirming(false)}
                  disabled={isSubmitting}
                  data-testid={`district-staff-deactivate-cancel-${member.staffProfileId}`}
                >
                  Cancel
                </Button>
              </>
            ) : (
              <Button
                variant="danger"
                onClick={() => {
                  setError(null);
                  setIsConfirming(true);
                }}
                data-testid={`district-staff-deactivate-${member.staffProfileId}`}
              >
                Deactivate
              </Button>
            )
          ) : (
            <Button
              variant="secondary"
              onClick={handleReactivate}
              disabled={isSubmitting}
              data-testid={`district-staff-reactivate-${member.staffProfileId}`}
            >
              {isSubmitting ? 'Reactivating...' : 'Reactivate'}
            </Button>
          )}
        </div>
      </Card>
    </li>
  );
}
