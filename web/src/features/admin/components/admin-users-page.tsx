import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Search, Users, Send } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { Notice } from "@/components/ui/notice";
import { EmptyState } from "@/components/ui/empty-state";
import { PageLayout } from "@/components/ui/page-layout";
import { Table, type TableColumn } from "@/components/ui/table";
import { useToast } from "@/components/ui/toast";
import { usePageTitle } from "@/hooks/use-page-title";
import { formatDate } from "@/lib/format-date";
import { useUsers } from "../hooks/use-users";
import { inviteBetaUser } from "../api/admin-api";

type AdminUser = ReturnType<typeof useUsers>["users"][number];

export function AdminUsersPage() {
  const { t } = useTranslation(['admin', 'common']);
  usePageTitle(t('users.pageTitle'));
  const { users, isLoading, error, reload } = useUsers();
  const { show: showToast } = useToast();
  const [search, setSearch] = useState("");
  const [inviteEmail, setInviteEmail] = useState("");
  const [showInvite, setShowInvite] = useState(false);
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [isInviting, setIsInviting] = useState(false);

  const handleInvite = async () => {
    const trimmed = inviteEmail.trim();
    if (!trimmed || !trimmed.includes("@")) {
      setInviteError(t('users.inviteModal.invalidEmail'));
      return;
    }
    setIsInviting(true);
    setInviteError(null);
    try {
      await inviteBetaUser(trimmed);
      showToast({ message: t('users.inviteModal.sentToast', { email: trimmed }), variant: "success" });
      setInviteEmail("");
      setShowInvite(false);
    } catch {
      setInviteError(t('users.inviteModal.sendFailed'));
    } finally {
      setIsInviting(false);
    }
  };

  const filtered = users.filter((u) => {
    const q = search.toLowerCase();
    return (
      u.firstName.toLowerCase().includes(q) ||
      u.lastName.toLowerCase().includes(q) ||
      u.email.toLowerCase().includes(q)
    );
  });

  const columns: TableColumn<AdminUser>[] = [
    {
      key: "name",
      header: t('common.column.name'),
      cell: (u) => `${u.firstName} ${u.lastName}`.trim(),
      sortValue: (u) => `${u.firstName} ${u.lastName}`.toLowerCase(),
    },
    {
      key: "email",
      header: t('common.column.email'),
      hideBelow: "md",
      cell: (u) => u.email,
      sortValue: (u) => u.email,
    },
    {
      key: "role",
      header: t('users.column.role'),
      cell: (u) => (
        <Badge variant={u.role === "Admin" ? "success" : "neutral"}>
          {u.role}
        </Badge>
      ),
      sortValue: (u) => u.role,
    },
    {
      key: "status",
      header: t('common.column.status'),
      cell: (u) => (
        <Badge variant={u.isActive ? "success" : "error"}>
          {u.isActive ? t('common.status.active') : t('common.status.inactive')}
        </Badge>
      ),
      sortValue: (u) => (u.isActive ? 0 : 1),
    },
    {
      key: "created",
      header: t('common.column.joined'),
      align: "right",
      hideBelow: "lg",
      cell: (u) => formatDate(u.createdAt),
      sortValue: (u) => u.createdAt,
    },
  ];

  return (
    <PageLayout
      title={t('users.pageTitle')}
      subtitle={t('users.subtitle', { count: filtered.length })}
      actions={
        <Button
          onClick={() => setShowInvite(true)}
          data-testid="admin-invite-button"
        >
          <Send
            size={14}
            strokeWidth={1.8}
            className="mr-1.5"
            aria-hidden="true"
          />
          {t('users.inviteButton')}
        </Button>
      }
    >
      <div className="relative">
        <Search
          size={16}
          strokeWidth={1.8}
          className="absolute left-3 top-1/2 -translate-y-1/2 text-brand-slate-400"
        />
        <Input
          placeholder={t('users.searchPlaceholder')}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="pl-9"
          data-testid="admin-user-search"
        />
      </div>

      {error && (
        <Notice variant="error" title={error}>
          <Button
            variant="secondary"
            size="sm"
            onClick={reload}
            className="mt-3"
          >
            {t('common:ui.tryAgain')}
          </Button>
        </Notice>
      )}

      <Table
        label={t('users.tableLabel')}
        data-testid="admin-users-table"
        columns={columns}
        rows={filtered}
        rowKey={(u) => u.id}
        rowHref={(u) => `/admin/users/${u.id}`}
        loading={isLoading}
        defaultSort={{ key: "name", direction: "asc" }}
        empty={<EmptyState icon={Users} title={t('users.emptyTitle')} />}
      />

      <Modal
        open={showInvite}
        onClose={() => setShowInvite(false)}
        title={t('users.inviteModal.title')}
        data-testid="admin-invite-modal"
      >
        <div className="space-y-3">
          <p className="text-sm text-brand-slate-500">{t('users.inviteModal.description')}</p>
          {inviteError && <Notice variant="error" title={inviteError} />}
          <Input
            placeholder={t('users.inviteModal.emailPlaceholder')}
            type="email"
            label={t('users.inviteModal.emailLabel')}
            value={inviteEmail}
            onChange={(e) => setInviteEmail(e.target.value)}
            data-testid="admin-invite-email"
          />
          <div className="flex justify-end">
            <Button
              onClick={handleInvite}
              loading={isInviting}
              disabled={!inviteEmail}
              data-testid="admin-send-invite"
            >
              {t('users.inviteModal.submit')}
            </Button>
          </div>
        </div>
      </Modal>
    </PageLayout>
  );
}
