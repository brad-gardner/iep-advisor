import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { Select } from '@/components/ui/input';
import { PageLayout } from '@/components/ui/page-layout';
import { useToast } from '@/components/ui/toast';
import type { AdminUser } from '@/types/api';
import { getUser, updateUser } from '../api/admin-api';

export function AdminUserDetail() {
  const { id } = useParams<{ id: string }>();
  const { show: showToast } = useToast();
  const [user, setUser] = useState<AdminUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  // Which mutation is in flight, so only the pressed button shows its spinner
  // while both stay disabled to prevent concurrent edits.
  const [savingAction, setSavingAction] = useState<null | 'save' | 'toggle'>(null);
  const saving = savingAction !== null;

  // Editable fields
  const [role, setRole] = useState('');
  const [isActive, setIsActive] = useState(true);
  // Bumped by the retry button to re-run the fetch effect. The effect body is an
  // inline async IIFE that only setStates after an await, keeping it effect-safe.
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!id) return;
    let active = true;
    (async () => {
      try {
        const data = await getUser(Number(id));
        if (!active) return;
        setUser(data);
        setRole(data.role);
        setIsActive(data.isActive);
        setError(null);
      } catch {
        if (active) setError('Failed to load user.');
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [id, reloadKey]);

  const retry = () => {
    setIsLoading(true);
    setError(null);
    setReloadKey((k) => k + 1);
  };

  const handleSave = async () => {
    if (!user) return;
    setSavingAction('save');
    setError(null);
    try {
      const updated = await updateUser(user.id, { role, isActive });
      setUser(updated);
      setRole(updated.role);
      setIsActive(updated.isActive);
      showToast({ message: 'User updated successfully.', variant: 'success' });
    } catch {
      setError('Failed to update user.');
    } finally {
      setSavingAction(null);
    }
  };

  const handleToggleActive = async () => {
    if (!user) return;
    setSavingAction('toggle');
    setError(null);
    try {
      const newActive = !user.isActive;
      const updated = await updateUser(user.id, { isActive: newActive });
      setUser(updated);
      setRole(updated.role);
      setIsActive(updated.isActive);
      showToast({
        message: newActive ? 'User reactivated.' : 'User deactivated.',
        variant: 'success',
      });
    } catch {
      setError('Failed to update user status.');
    } finally {
      setSavingAction(null);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading user…" />
      </div>
    );
  }

  if (error && !user) {
    return (
      <div>
        <Notice variant="error" title={error}>
          <Button variant="secondary" size="sm" onClick={retry} className="mt-3">
            Retry
          </Button>
        </Notice>
        <Link
          to="/admin/users"
          className="inline-flex items-center gap-1.5 text-sm text-brand-teal-500 hover:text-brand-teal-600 mt-4"
        >
          <ArrowLeft size={14} strokeWidth={1.8} aria-hidden="true" />
          Back to users
        </Link>
      </div>
    );
  }

  if (!user) return null;

  return (
    <PageLayout
      title={`${user.firstName} ${user.lastName}`}
      breadcrumb={[
        { label: 'Users', to: '/admin/users' },
        { label: `${user.firstName} ${user.lastName}` },
      ]}
    >
      {error && <Notice variant="error" title={error} />}

      <Card>
        <div className="space-y-4">
          <InfoRow label="Email" value={user.email} />
          <InfoRow label="State" value={user.state ?? 'Not set'} />
          <InfoRow
            label="Status"
            value={
              <Badge variant={user.isActive ? 'success' : 'error'}>
                {user.isActive ? 'Active' : 'Inactive'}
              </Badge>
            }
          />
          <InfoRow
            label="Joined"
            value={new Date(user.createdAt).toLocaleDateString()}
          />
        </div>
      </Card>

      <Card>
        <h2 className="text-sm font-medium text-brand-slate-800 mb-4">Edit User</h2>
        <div className="space-y-4">
          <Select
            label="Role"
            value={role}
            onChange={(e) => setRole((e.target as HTMLSelectElement).value)}
            data-testid="admin-user-role"
          >
            <option value="User">User</option>
            <option value="Admin">Admin</option>
          </Select>

          <div className="flex items-center gap-3">
            <span className="text-[13px] font-medium text-brand-slate-600">Active</span>
            <button
              type="button"
              role="switch"
              aria-checked={isActive}
              aria-label="Active"
              onClick={() => setIsActive(!isActive)}
              data-testid="admin-user-active"
              className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors ${
                isActive ? 'bg-brand-teal-500' : 'bg-brand-slate-300'
              }`}
            >
              <span
                className={`pointer-events-none inline-block h-4 w-4 rounded-full bg-white shadow transform transition-transform ${
                  isActive ? 'translate-x-4' : 'translate-x-0'
                }`}
              />
            </button>
          </div>

          <div className="pt-2">
            <Button
              onClick={handleSave}
              loading={savingAction === 'save'}
              disabled={saving}
              data-testid="admin-user-save"
            >
              Save Changes
            </Button>
          </div>
        </div>
      </Card>

      <Card>
        <h2 className="text-sm font-medium text-brand-slate-800 mb-3">Actions</h2>
        {user.isActive ? (
          <Button
            variant="danger"
            onClick={handleToggleActive}
            loading={savingAction === 'toggle'}
            disabled={saving}
          >
            Deactivate User
          </Button>
        ) : (
          <Button
            variant="secondary"
            onClick={handleToggleActive}
            loading={savingAction === 'toggle'}
            disabled={saving}
          >
            Reactivate User
          </Button>
        )}
      </Card>
    </PageLayout>
  );
}

function InfoRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-[13px] text-brand-slate-500">{label}</span>
      <span className="text-sm text-brand-slate-800">{value}</span>
    </div>
  );
}
