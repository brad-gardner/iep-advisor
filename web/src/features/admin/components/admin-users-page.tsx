import { useState } from "react";
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
import { useUsers } from "../hooks/use-users";
import { inviteBetaUser } from "../api/admin-api";

type AdminUser = ReturnType<typeof useUsers>["users"][number];

export function AdminUsersPage() {
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
      setInviteError("Please enter a valid email address");
      return;
    }
    setIsInviting(true);
    setInviteError(null);
    try {
      await inviteBetaUser(trimmed);
      showToast({ message: `Invite sent to ${trimmed}`, variant: "success" });
      setInviteEmail("");
      setShowInvite(false);
    } catch {
      setInviteError("Failed to send invite");
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
      header: "Name",
      cell: (u) => `${u.firstName} ${u.lastName}`.trim(),
      sortValue: (u) => `${u.firstName} ${u.lastName}`.toLowerCase(),
    },
    {
      key: "email",
      header: "Email",
      hideBelow: "md",
      cell: (u) => u.email,
      sortValue: (u) => u.email,
    },
    {
      key: "role",
      header: "Role",
      cell: (u) => (
        <Badge variant={u.role === "Admin" ? "success" : "neutral"}>
          {u.role}
        </Badge>
      ),
      sortValue: (u) => u.role,
    },
    {
      key: "status",
      header: "Status",
      cell: (u) => (
        <Badge variant={u.isActive ? "success" : "error"}>
          {u.isActive ? "Active" : "Inactive"}
        </Badge>
      ),
      sortValue: (u) => (u.isActive ? 0 : 1),
    },
    {
      key: "created",
      header: "Joined",
      align: "right",
      hideBelow: "lg",
      cell: (u) => new Date(u.createdAt).toLocaleDateString(),
      sortValue: (u) => u.createdAt,
    },
  ];

  return (
    <PageLayout
      title="User Management"
      subtitle={`${filtered.length} user${filtered.length !== 1 ? "s" : ""}`}
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
          Invite Beta User
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
          placeholder="Search by name or email..."
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
            Retry
          </Button>
        </Notice>
      )}

      <Table
        label="Users"
        data-testid="admin-users-table"
        columns={columns}
        rows={filtered}
        rowKey={(u) => u.id}
        rowHref={(u) => `/admin/users/${u.id}`}
        loading={isLoading}
        defaultSort={{ key: "name", direction: "asc" }}
        empty={<EmptyState icon={Users} title="No users found." />}
      />

      <Modal
        open={showInvite}
        onClose={() => setShowInvite(false)}
        title="Invite Beta User"
        data-testid="admin-invite-modal"
      >
        <div className="space-y-3">
          <p className="text-sm text-brand-slate-500">
            Enter their email. They'll receive a signup link with a beta code
            that auto-fills on the registration page.
          </p>
          {inviteError && <Notice variant="error" title={inviteError} />}
          <Input
            placeholder="email@example.com"
            type="email"
            label="Email"
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
              Send Invite
            </Button>
          </div>
        </div>
      </Modal>
    </PageLayout>
  );
}
