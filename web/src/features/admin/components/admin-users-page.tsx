import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, Users, Send } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { useUsers } from '../hooks/use-users';
import { inviteBetaUser } from '../api/admin-api';

export function AdminUsersPage() {
  const { users, isLoading, error } = useUsers();
  const [search, setSearch] = useState('');
  const [inviteEmail, setInviteEmail] = useState('');
  const [showInvite, setShowInvite] = useState(false);
  const [inviteResult, setInviteResult] = useState<{ success: boolean; message: string } | null>(null);
  const [isInviting, setIsInviting] = useState(false);
  const navigate = useNavigate();

  const handleInvite = async () => {
    const trimmed = inviteEmail.trim();
    if (!trimmed || !trimmed.includes('@')) {
      setInviteResult({ success: false, message: 'Please enter a valid email address' });
      return;
    }
    setIsInviting(true);
    setInviteResult(null);
    try {
      await inviteBetaUser(trimmed);
      setInviteResult({ success: true, message: `Invite sent to ${inviteEmail}` });
      setInviteEmail('');
      setShowInvite(false);
    } catch {
      setInviteResult({ success: false, message: 'Failed to send invite' });
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
    <div className="max-w-4xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-serif text-brand-slate-800">User Management</h1>
        <div className="flex items-center gap-3">
          <span className="text-sm text-brand-slate-500">
            {filtered.length} user{filtered.length !== 1 ? 's' : ''}
          </span>
          <Button onClick={() => setShowInvite(!showInvite)} data-testid="admin-invite-button">
            <Send size={14} strokeWidth={1.8} className="mr-1.5" aria-hidden="true" />
            Invite Beta User
          </Button>
        </div>
      </div>

      {inviteResult && (
        <div className="mb-4">
          <Notice variant={inviteResult.success ? 'success' : 'error'} title={inviteResult.message} />
        </div>
      )}

      {showInvite && (
        <Card className="mb-6">
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
            <Button onClick={handleInvite} disabled={!inviteEmail || isInviting} data-testid="admin-send-invite">
              {isInviting ? 'Sending...' : 'Send Invite'}
            </Button>
          </div>
        </Card>
      )}

      <div className="relative mb-6">
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

      {error && <Notice variant="error" title={error} />}

      {isLoading ? (
        <div className="flex justify-center py-12">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
        </div>
      ) : filtered.length === 0 ? (
        <Card className="text-center py-12">
          <Users size={32} strokeWidth={1.8} className="mx-auto text-brand-slate-300 mb-3" />
          <p className="text-sm text-brand-slate-500">No users found.</p>
        </Card>
      ) : (
        <div className="space-y-2">
          {filtered.map((u) => (
            <UserRow
              key={u.id}
              user={u}
              onClick={() => navigate(`/admin/users/${u.id}`)}
            />
          ))}
        </div>
      )}
    </div>
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
    <Card
      className="cursor-pointer hover:border-brand-teal-200 transition-colors"
    >
      <button
        type="button"
        onClick={onClick}
        className="w-full flex items-center justify-between gap-4 text-left"
      >
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium text-brand-slate-800 truncate">
            {user.firstName} {user.lastName}
          </p>
          <p className="text-xs text-brand-slate-500 truncate">{user.email}</p>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <Badge variant={user.role === 'Admin' ? 'success' : 'neutral'}>
            {user.role}
          </Badge>
          <Badge variant={user.isActive ? 'success' : 'error'}>
            {user.isActive ? 'Active' : 'Inactive'}
          </Badge>
          <span className="text-xs text-brand-slate-400 hidden sm:inline">
            {new Date(user.createdAt).toLocaleDateString()}
          </span>
        </div>
      </button>
    </Card>
  );
}
