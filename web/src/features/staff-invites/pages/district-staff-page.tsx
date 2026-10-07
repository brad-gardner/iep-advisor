import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Plus, Ban, RotateCcw, Send, Trash2, Upload } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { EmptyState } from "@/components/ui/empty-state";
import { Modal } from "@/components/ui/modal";
import { Spinner } from "@/components/ui/spinner";
import { PageLayout } from "@/components/ui/page-layout";
import { Table, type TableColumn } from "@/components/ui/table";
import { useToast } from "@/components/ui/toast";
import { usePageTitle } from "@/hooks/use-page-title";
import { formatDate } from "@/lib/format-date";
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

export function DistrictStaffPage() {
  const { t } = useTranslation('staff-invites');
  usePageTitle(t('districtStaffPage.title'));
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
          message: t('districtStaffPage.toasts.inviteSent', { email: data.email }),
          variant: "success",
        });
        return { success: true, invite: response.data };
      }
      return {
        success: false,
        error: response.message || t('districtStaffPage.errors.sendFailed'),
      };
    } catch {
      return { success: false, error: t('districtStaffPage.errors.generic') };
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
        showToast({ message: t('districtStaffPage.toasts.inviteRevoked'), variant: "success" });
      } else {
        setRevokeError(response.message || t('districtStaffPage.errors.revokeFailed'));
      }
    } catch {
      setRevokeError(t('districtStaffPage.errors.generic'));
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
          message: t('districtStaffPage.toasts.inviteResent', { email: invite.email }),
          variant: "success",
        });
        if (response.data?.inviteUrl) setResendUrl(response.data.inviteUrl);
      } else {
        showToast({
          message: response.message || t('districtStaffPage.errors.resendFailed'),
          variant: "error",
        });
      }
    } catch {
      showToast({ message: t('districtStaffPage.errors.generic'), variant: "error" });
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
        showToast({ message: t('districtStaffPage.toasts.staffDeactivated'), variant: "success" });
      } else {
        // Backend returns an explicit message for the last-DistrictAdmin guard.
        setDeactivateError(
          response.message || t('districtStaffPage.errors.deactivateFailed'),
        );
      }
    } catch {
      setDeactivateError(t('districtStaffPage.errors.generic'));
    } finally {
      setIsDeactivating(false);
    }
  };

  const handleReactivate = async (member: StaffMember) => {
    try {
      const response = await reactivateStaff(member.staffProfileId);
      if (response.success) {
        await reloadStaff();
        showToast({ message: t('districtStaffPage.toasts.staffReactivated'), variant: "success" });
      } else {
        showToast({
          message: response.message || t('districtStaffPage.errors.reactivateFailed'),
          variant: "error",
        });
      }
    } catch {
      showToast({ message: t('districtStaffPage.errors.generic'), variant: "error" });
    }
  };

  const staffColumns: TableColumn<StaffMember>[] = [
    {
      key: "name",
      header: t('districtStaffPage.columns.name'),
      cell: (m) => (
        <div className="flex items-center gap-2">
          <span className="font-medium text-brand-slate-800">
            {`${m.firstName} ${m.lastName}`.trim() || m.email}
          </span>
          <Badge variant={m.isActive ? "success" : "neutral"}>
            {m.isActive ? t('districtStaffPage.statusActive') : t('districtStaffPage.statusInactive')}
          </Badge>
        </div>
      ),
      sortValue: (m) => `${m.firstName} ${m.lastName}`.toLowerCase(),
    },
    {
      key: "email",
      header: t('districtStaffPage.columns.email'),
      hideBelow: "md",
      cell: (m) => m.email,
      sortValue: (m) => m.email,
    },
    {
      key: "role",
      header: t('districtStaffPage.columns.role'),
      hideBelow: "lg",
      cell: (m) =>
        `${orgRoleLabel(m.orgRoleName)}${m.schoolName ? ` · ${m.schoolName}` : ` · ${t('districtStaffPage.districtWide')}`}`,
      sortValue: (m) => m.orgRoleName,
    },
  ];

  const inviteColumns: TableColumn<StaffPendingInvite>[] = [
    {
      key: "email",
      header: t('districtStaffPage.columns.email'),
      cell: (i) => (
        <div className="flex items-center gap-2">
          <span className="font-medium text-brand-slate-800">{i.email}</span>
          {i.status === "expired" && <Badge variant="error">{t('districtStaffPage.expiredBadge')}</Badge>}
        </div>
      ),
      sortValue: (i) => i.email,
    },
    {
      key: "role",
      header: t('districtStaffPage.columns.role'),
      hideBelow: "md",
      cell: (i) =>
        `${orgRoleLabel(i.orgRoleName)}${i.schoolName ? ` · ${i.schoolName}` : ` · ${t('districtStaffPage.districtWide')}`}`,
      sortValue: (i) => i.orgRoleName,
    },
    {
      key: "expires",
      header: t('districtStaffPage.columns.expires'),
      align: "right",
      hideBelow: "lg",
      cell: (i) => formatDate(i.inviteExpiresAt, ""),
      sortValue: (i) => i.inviteExpiresAt,
    },
  ];

  return (
    <PageLayout
      title={t('districtStaffPage.title')}
      data-testid="district-staff-page"
      actions={
        profile && (
          <div className="flex flex-wrap gap-2">
            <Link to="/educator/admin/imports?kind=Staff">
              <Button variant="secondary" data-testid="district-staff-import-link">
                <Upload className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
                {t('districtStaffPage.importStaff')}
              </Button>
            </Link>
            <Button
              onClick={() => setIsInviteOpen(true)}
              data-testid="district-staff-invite-open"
            >
              <Plus className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
              {t('districtStaffPage.inviteStaff')}
            </Button>
          </div>
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
            <h2 className="font-serif text-lg">{t('districtStaffPage.pendingInvitesHeading')}</h2>
            <Table
              label={t('districtStaffPage.pendingInvitesHeading')}
              data-testid="staff-invites-list"
              columns={inviteColumns}
              rows={staff.pendingInvites}
              rowKey={(i) => i.id}
              defaultSort={{ key: "email", direction: "asc" }}
              rowActionLabel={(i) => i.email}
              rowActions={(i) => [
                {
                  label: t('districtStaffPage.resend'),
                  icon: <Send className="h-3.5 w-3.5" strokeWidth={1.8} />,
                  onSelect: () => handleResend(i),
                  "data-testid": `staff-invite-resend-${i.id}`,
                },
                {
                  label: t('districtStaffPage.revoke'),
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
                  title={t('districtStaffPage.pendingEmptyTitle')}
                  description={t('districtStaffPage.pendingEmptyDescription')}
                />
              }
            />
          </section>

          <section className="space-y-3">
            <h2 className="font-serif text-lg">{t('districtStaffPage.staffHeading')}</h2>
            {solelyOwned && (
              <DeactivateSolelyOwnedNotice
                result={solelyOwned}
                onDismiss={() => setSolelyOwned(null)}
              />
            )}
            <Table
              label={t('districtStaffPage.staffHeading')}
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
                        label: t('districtStaffPage.deactivate'),
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
                        label: t('districtStaffPage.reactivate'),
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
                  title={t('districtStaffPage.staffEmptyTitle')}
                  description={t('districtStaffPage.staffEmptyDescription')}
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
          title={t('districtStaffPage.inviteModalTitle')}
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
        title={t('districtStaffPage.deactivateDialogTitle')}
        message={
          deactivating
            ? t('districtStaffPage.deactivateMessage', {
                name: `${deactivating.firstName} ${deactivating.lastName}`.trim() || deactivating.email,
              })
            : ""
        }
        confirmLabel={t('districtStaffPage.deactivateConfirmLabel')}
        loading={isDeactivating}
        error={deactivateError}
        onConfirm={confirmDeactivate}
        onCancel={() => setDeactivating(null)}
        data-testid="district-staff-deactivate-dialog"
      />

      <ConfirmDialog
        open={revoking !== null}
        title={t('districtStaffPage.revokeDialogTitle')}
        message={revoking ? t('districtStaffPage.revokeMessage', { email: revoking.email }) : ""}
        confirmLabel={t('districtStaffPage.revokeConfirmLabel')}
        loading={isRevoking}
        error={revokeError}
        onConfirm={confirmRevoke}
        onCancel={() => setRevoking(null)}
        data-testid="staff-invite-revoke-dialog"
      />

      <Modal
        open={resendUrl !== null}
        onClose={() => setResendUrl(null)}
        title={t('districtStaffPage.resendUrlModalTitle')}
        size="sm"
        data-testid="staff-resend-url-modal"
      >
        {resendUrl && <InviteUrlField url={resendUrl} />}
      </Modal>
    </PageLayout>
  );
}
