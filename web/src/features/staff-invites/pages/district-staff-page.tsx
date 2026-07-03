import { useCallback, useEffect, useState } from "react";
import { Plus, Ban, RotateCcw, Send, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { EmptyState } from "@/components/ui/empty-state";
import { Modal } from "@/components/ui/modal";
import { Spinner } from "@/components/ui/spinner";
import { PageLayout } from "@/components/ui/page-layout";
import { Table, type TableColumn } from "@/components/ui/table";
import { useToast } from "@/components/ui/toast";
import { orgRoleLabel } from "@/lib/org-role-label";
import { useEducatorProfile } from "@/features/educator/hooks/use-educator-profile";
import { getDistrictSchools } from "@/features/district-admin/api/district-api";
import type { DistrictSchool } from "@/features/district-admin/types";
import {
  createStaffInvite,
  deactivateStaff,
  getStaffList,
  reactivateStaff,
  resendStaffInvite,
  revokeStaffInvite,
} from "../api/staff-invites-api";
import { InviteForm } from "../components/invite-form";
import { InviteUrlField } from "../components/invite-url-field";
import { DeactivateSolelyOwnedNotice } from "../components/deactivate-solely-owned-notice";
import type {
  CreateStaffInviteRequest,
  DeactivateStaffResponse,
  StaffList as StaffListData,
  StaffMember,
  StaffPendingInvite,
} from "../types";

const EMPTY_LIST: StaffListData = { members: [], pendingInvites: [] };

function formatExpiry(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

export function DistrictStaffPage() {
  const { profile } = useEducatorProfile();
  const { show: showToast } = useToast();
  const [staff, setStaff] = useState<StaffListData>(EMPTY_LIST);
  const [schools, setSchools] = useState<DistrictSchool[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isInviteOpen, setIsInviteOpen] = useState(false);
  // After a deactivate, surface students that were only accessible to that staff
  // member so an admin can reassign them.
  const [solelyOwned, setSolelyOwned] =
    useState<DeactivateStaffResponse | null>(null);
  const [deactivating, setDeactivating] = useState<StaffMember | null>(null);
  const [deactivateError, setDeactivateError] = useState<string | null>(null);
  const [isDeactivating, setIsDeactivating] = useState(false);
  const [revoking, setRevoking] = useState<StaffPendingInvite | null>(null);
  const [revokeError, setRevokeError] = useState<string | null>(null);
  const [isRevoking, setIsRevoking] = useState(false);
  // Dev-only invite link surfaced after a resend.
  const [resendUrl, setResendUrl] = useState<string | null>(null);

  const reloadStaff = useCallback(async () => {
    try {
      const response = await getStaffList();
      setStaff(response.success && response.data ? response.data : EMPTY_LIST);
    } catch {
      setStaff(EMPTY_LIST);
    }
  }, []);

  const reloadSchools = useCallback(async () => {
    try {
      const response = await getDistrictSchools();
      setSchools(response.success && response.data ? response.data : []);
    } catch {
      setSchools([]);
    }
  }, []);

  useEffect(() => {
    let active = true;
    (async () => {
      await Promise.all([reloadStaff(), reloadSchools()]);
      if (active) setIsLoading(false);
    })();
    return () => {
      active = false;
    };
  }, [reloadStaff, reloadSchools]);

  const handleInvite = async (data: CreateStaffInviteRequest) => {
    try {
      const response = await createStaffInvite(data);
      if (response.success && response.data) {
        await reloadStaff();
        showToast({
          message: `Invite sent to ${data.email}`,
          variant: "success",
        });
        return { success: true, invite: response.data };
      }
      return {
        success: false,
        error: response.message || "Failed to send invite",
      };
    } catch {
      return { success: false, error: "An error occurred" };
    }
  };

  const confirmRevoke = async () => {
    if (!revoking) return;
    setIsRevoking(true);
    setRevokeError(null);
    try {
      const response = await revokeStaffInvite(revoking.id);
      if (response.success) {
        await reloadStaff();
        setRevoking(null);
        showToast({ message: "Invite revoked", variant: "success" });
      } else {
        setRevokeError(response.message || "Failed to revoke invite");
      }
    } catch {
      setRevokeError("An error occurred");
    } finally {
      setIsRevoking(false);
    }
  };

  const handleResend = async (invite: StaffPendingInvite) => {
    try {
      const response = await resendStaffInvite(invite.id);
      if (response.success) {
        await reloadStaff();
        showToast({
          message: `Invite resent to ${invite.email}`,
          variant: "success",
        });
        if (response.data?.inviteUrl) setResendUrl(response.data.inviteUrl);
      } else {
        showToast({
          message: response.message || "Failed to resend invite",
          variant: "error",
        });
      }
    } catch {
      showToast({ message: "An error occurred", variant: "error" });
    }
  };

  const confirmDeactivate = async () => {
    if (!deactivating) return;
    setIsDeactivating(true);
    setDeactivateError(null);
    setSolelyOwned(null);
    try {
      const response = await deactivateStaff(deactivating.staffProfileId);
      if (response.success) {
        await reloadStaff();
        if (response.data && response.data.solelyOwnedStudentCount > 0) {
          setSolelyOwned(response.data);
        }
        setDeactivating(null);
        showToast({ message: "Staff member deactivated", variant: "success" });
      } else {
        // Backend returns an explicit message for the last-DistrictAdmin guard.
        setDeactivateError(
          response.message ||
            "This staff member cannot be deactivated right now",
        );
      }
    } catch {
      setDeactivateError("An error occurred");
    } finally {
      setIsDeactivating(false);
    }
  };

  const handleReactivate = async (member: StaffMember) => {
    try {
      const response = await reactivateStaff(member.staffProfileId);
      if (response.success) {
        await reloadStaff();
        showToast({ message: "Staff member reactivated", variant: "success" });
      } else {
        showToast({
          message: response.message || "Failed to reactivate",
          variant: "error",
        });
      }
    } catch {
      showToast({ message: "An error occurred", variant: "error" });
    }
  };

  const staffColumns: TableColumn<StaffMember>[] = [
    {
      key: "name",
      header: "Name",
      cell: (m) => (
        <div className="flex items-center gap-2">
          <span className="font-medium text-brand-slate-800">
            {`${m.firstName} ${m.lastName}`.trim() || m.email}
          </span>
          <Badge variant={m.isActive ? "success" : "neutral"}>
            {m.isActive ? "Active" : "Inactive"}
          </Badge>
        </div>
      ),
      sortValue: (m) => `${m.firstName} ${m.lastName}`.toLowerCase(),
    },
    {
      key: "email",
      header: "Email",
      hideBelow: "md",
      cell: (m) => m.email,
      sortValue: (m) => m.email,
    },
    {
      key: "role",
      header: "Role",
      hideBelow: "lg",
      cell: (m) =>
        `${orgRoleLabel(m.orgRoleName)}${m.schoolName ? ` · ${m.schoolName}` : " · District-wide"}`,
      sortValue: (m) => m.orgRoleName,
    },
  ];

  const inviteColumns: TableColumn<StaffPendingInvite>[] = [
    {
      key: "email",
      header: "Email",
      cell: (i) => (
        <div className="flex items-center gap-2">
          <span className="font-medium text-brand-slate-800">{i.email}</span>
          {i.status === "expired" && <Badge variant="error">Expired</Badge>}
        </div>
      ),
      sortValue: (i) => i.email,
    },
    {
      key: "role",
      header: "Role",
      hideBelow: "md",
      cell: (i) =>
        `${orgRoleLabel(i.orgRoleName)}${i.schoolName ? ` · ${i.schoolName}` : " · District-wide"}`,
      sortValue: (i) => i.orgRoleName,
    },
    {
      key: "expires",
      header: "Expires",
      align: "right",
      hideBelow: "lg",
      cell: (i) => formatExpiry(i.inviteExpiresAt),
      sortValue: (i) => i.inviteExpiresAt,
    },
  ];

  return (
    <PageLayout
      title="Staff"
      data-testid="district-staff-page"
      actions={
        profile && (
          <Button
            onClick={() => setIsInviteOpen(true)}
            data-testid="district-staff-invite-open"
          >
            <Plus className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
            Invite staff
          </Button>
        )
      }
    >
      {isLoading ? (
        <div className="flex justify-center py-12">
          <Spinner />
        </div>
      ) : (
        <>
          <section className="space-y-3">
            <h2 className="font-serif text-lg">Pending invites</h2>
            <Table
              label="Pending invites"
              data-testid="staff-invites-list"
              columns={inviteColumns}
              rows={staff.pendingInvites}
              rowKey={(i) => i.id}
              defaultSort={{ key: "email", direction: "asc" }}
              rowActionLabel={(i) => i.email}
              rowActions={(i) => [
                {
                  label: "Resend",
                  icon: <Send className="h-3.5 w-3.5" strokeWidth={1.8} />,
                  onSelect: () => handleResend(i),
                  "data-testid": `staff-invite-resend-${i.id}`,
                },
                {
                  label: "Revoke",
                  icon: <Trash2 className="h-3.5 w-3.5" strokeWidth={1.8} />,
                  variant: "danger",
                  onSelect: () => {
                    setRevokeError(null);
                    setRevoking(i);
                  },
                  "data-testid": `staff-invite-revoke-${i.id}`,
                },
              ]}
              empty={
                <EmptyState
                  data-testid="staff-invites-empty"
                  icon={Send}
                  title="No pending invites"
                  description="Invited staff appear here until they accept."
                />
              }
            />
          </section>

          <section className="space-y-3">
            <h2 className="font-serif text-lg">Staff</h2>
            {solelyOwned && (
              <DeactivateSolelyOwnedNotice
                result={solelyOwned}
                onDismiss={() => setSolelyOwned(null)}
              />
            )}
            <Table
              label="Staff"
              data-testid="district-staff-list"
              columns={staffColumns}
              rows={staff.members}
              rowKey={(m) => m.staffProfileId}
              defaultSort={{ key: "name", direction: "asc" }}
              rowActionLabel={(m) =>
                `${m.firstName} ${m.lastName}`.trim() || m.email
              }
              rowActions={(m) =>
                m.isActive
                  ? [
                      {
                        label: "Deactivate",
                        icon: <Ban className="h-3.5 w-3.5" strokeWidth={1.8} />,
                        variant: "danger",
                        onSelect: () => {
                          setDeactivateError(null);
                          setDeactivating(m);
                        },
                        "data-testid": `district-staff-deactivate-${m.staffProfileId}`,
                      },
                    ]
                  : [
                      {
                        label: "Reactivate",
                        icon: (
                          <RotateCcw
                            className="h-3.5 w-3.5"
                            strokeWidth={1.8}
                          />
                        ),
                        onSelect: () => handleReactivate(m),
                        "data-testid": `district-staff-reactivate-${m.staffProfileId}`,
                      },
                    ]
              }
              empty={
                <EmptyState
                  data-testid="district-staff-empty"
                  icon={Plus}
                  title="No staff yet"
                  description="Invite school admins and teachers to get started."
                />
              }
            />
          </section>
        </>
      )}

      {profile && (
        <Modal
          open={isInviteOpen}
          onClose={() => setIsInviteOpen(false)}
          title="Invite a staff member"
          data-testid="district-staff-invite-modal"
        >
          <InviteForm
            callerOrgRoleId={profile.orgRoleId}
            callerSchoolId={profile.schoolId}
            schools={schools}
            onSubmit={async (data) => {
              const result = await handleInvite(data);
              if (result.success) setIsInviteOpen(false);
              return result;
            }}
          />
        </Modal>
      )}

      <ConfirmDialog
        open={deactivating !== null}
        title="Deactivate staff member"
        message={
          deactivating
            ? `Deactivate ${`${deactivating.firstName} ${deactivating.lastName}`.trim() || deactivating.email}? They will lose access until reactivated.`
            : ""
        }
        confirmLabel="Deactivate"
        loading={isDeactivating}
        error={deactivateError}
        onConfirm={confirmDeactivate}
        onCancel={() => setDeactivating(null)}
        data-testid="district-staff-deactivate-dialog"
      />

      <ConfirmDialog
        open={revoking !== null}
        title="Revoke invite"
        message={revoking ? `Revoke the invite sent to ${revoking.email}?` : ""}
        confirmLabel="Revoke invite"
        loading={isRevoking}
        error={revokeError}
        onConfirm={confirmRevoke}
        onCancel={() => setRevoking(null)}
        data-testid="staff-invite-revoke-dialog"
      />

      <Modal
        open={resendUrl !== null}
        onClose={() => setResendUrl(null)}
        title="Invite link"
        size="sm"
        data-testid="staff-resend-url-modal"
      >
        {resendUrl && <InviteUrlField url={resendUrl} />}
      </Modal>
    </PageLayout>
  );
}
