import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, Users, Send } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { EmptyState } from '@/components/ui/empty-state';
import { PageLayout } from '@/components/ui/page-layout';
import { useToast } from '@/components/ui/toast';
import { useUsers } from '../hooks/use-users';
import { inviteBetaUser } from '../api/admin-api';

export function AdminUsersPage() {
  const { users, isLoading, error, reload } = useUsers();
  const { show: showToast } = useToast();
  const [search, setSearch] = useState('');
  const [inviteEmail, setInviteEmail] = useState('');
  const [showInvite, setShowInvite] = useState(false);
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [isInviting, setIsInviting] = useState(false);
  const navigate = useNavigate();

  const handleInvite = async () => {
    const trimmed = inviteEmail.trim();
    if (!trimmed || !trimmed.includes('@')) {
      setInviteError('Please enter a valid email address');
      return;
    }
    setIsInviting(true);
    setInviteError(null);
    try {
      await inviteBetaUser(trimmed);
      showToast({ message: `Invite sent to ${trimmed}`, variant: 'success' });
      setInviteEmail('');
      setShowInvite(false);
    } catch {
      setInviteError('Failed to send invite');
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

  return (
    <PageLayout
      title="User Management"
      actions={
        <>
          <span className="text-sm text-brand-slate-500">
            {filtered.length} user{filtered.length !== 1 ? 's' : ''}
          </span>
          <Button
            onClick={() => setShowInvite(!showInvite)}
            data-testid="admin-invite-button"
            aria-expanded={showInvite}
            aria-controls="admin-invite-panel"
          >
            <Send size={14} strokeWidth={1.8} className="mr-1.5" aria-hidden="true" />
            Invite Beta User
          </Button>
        </>
      }
    >
      {inviteError && <Notice variant="error" title={inviteError} />}

      {showInvite && (
        <Card id="admin-invite-panel">
          <h3 className="font-serif text-brand-slate-800 mb-3">Invite Beta User</h3>
          <p className="text-sm text-brand-slate-400 mb-3">
            Enter their email. They'll receive a signup link with a beta code that auto-fills on the registration page.
          </p>
          <div className="flex gap-3">
            <Input
              placeholder="email@example.com"
              type="email"
              value={inviteEmail}
              onChange={(e) => setInviteEmail(e.target.value)}
              className="flex-1"
              data-testid="admin-invite-email"
            />
            <Button
              onClick={handleInvite}
              loading={isInviting}
              disabled={!inviteEmail}
              data-testid="admin-send-invite"
            >
              Send Invite
            </Button>
          </div>
        </Card>
      )}

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
          <Button variant="secondary" size="sm" onClick={reload} className="mt-3">
            Retry
          </Button>
        </Notice>
      )}

      {isLoading ? (
        <div className="flex justify-center py-12">
          <Spinner label="Loading users…" />
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState icon={Users} title="No users found." />
      ) : (
        <div className="divide-y divide-brand-slate-100 overflow-hidden rounded-card border border-brand-slate-200 bg-white">
          {filtered.map((u) => (
            <UserRow
              key={u.id}
              user={u}
              onClick={() => navigate(`/admin/users/${u.id}`)}
            />
          ))}
        </div>
      )}
    </PageLayout>
  );
}

interface UserRowProps {
  user: {
    id: number;
    firstName: string;
    lastName: string;
    email: string;
    role: string;
    isActive: boolean;
    createdAt: string;
  };
  onClick: () => void;
}

function UserRow({ user, onClick }: UserRowProps) {
  return (
    <Button
      variant="ghost"
      onClick={onClick}
      className="w-full rounded-card border border-brand-slate-200 bg-white px-6 py-4 hover:bg-brand-teal-50"
    >
      <span className="w-full flex items-center justify-between gap-4 text-left">
        <span className="min-w-0 flex-1">
          <span className="block text-sm font-medium text-brand-slate-800 truncate">
            {user.firstName} {user.lastName}
          </span>
          <span className="block text-xs text-brand-slate-500 truncate">{user.email}</span>
        </span>

        <span className="flex items-center gap-2 shrink-0">
          <Badge variant={user.role === 'Admin' ? 'success' : 'neutral'}>
            {user.role}
          </Badge>
          <Badge variant={user.isActive ? 'success' : 'error'}>
            {user.isActive ? 'Active' : 'Inactive'}
          </Badge>
          <span className="text-xs text-brand-slate-400 hidden sm:inline">
            {new Date(user.createdAt).toLocaleDateString()}
          </span>
        </span>
      </span>
    </Button>
  );
}
