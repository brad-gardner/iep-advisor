import { useState } from "react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { orgRoleLabel } from "@/lib/org-role-label";
import type { StudentStaffAccess } from "../../types";

interface StaffAccessRowProps {
  grant: StudentStaffAccess;
  // Admins (DistrictAdmin/SchoolAdmin) get a revoke control; teachers see the
  // row read-only.
  canManage: boolean;
  onRevoke: (accessId: number) => Promise<{ success: boolean; error?: string }>;
}

export function StaffAccessRow({
  grant,
  canManage,
  onRevoke,
}: StaffAccessRowProps) {
  const [isConfirming, setIsConfirming] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fullName = `${grant.firstName} ${grant.lastName}`.trim() || grant.email;

  const handleRevoke = async () => {
    setIsSubmitting(true);
    setError(null);
    const result = await onRevoke(grant.accessId);
    if (result.success) {
      setIsConfirming(false);
    } else {
      setError(result.error ?? "Could not revoke this access");
    }
    setIsSubmitting(false);
  };

  return (
    <li>
      <Card
        className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"
        data-testid={`student-staff-access-${grant.accessId}`}
      >
        <div className="space-y-1">
          <div className="flex items-center gap-2">
            <span className="text-brand-slate-800 font-medium">{fullName}</span>
            <Badge variant="info">{grant.accessRole}</Badge>
          </div>
          <p className="text-sm text-brand-slate-500">{grant.email}</p>
          <p className="text-xs text-brand-slate-400">
            {orgRoleLabel(grant.orgRoleName)}
          </p>
        </div>

        {canManage && (
          <div className="flex shrink-0 items-center gap-2">
            <Button
              variant="danger"
              onClick={() => {
                setError(null);
                setIsConfirming(true);
              }}
              data-testid={`student-staff-revoke-${grant.accessId}`}
            >
              Revoke
            </Button>
          </div>
        )}
      </Card>

      <ConfirmDialog
        open={isConfirming}
        title="Revoke staff access"
        message={`Revoke ${fullName}'s access to this student?`}
        confirmLabel="Revoke access"
        loading={isSubmitting}
        error={error}
        onConfirm={handleRevoke}
        onCancel={() => setIsConfirming(false)}
        data-testid={`student-staff-revoke-dialog-${grant.accessId}`}
      />
    </li>
  );
}
