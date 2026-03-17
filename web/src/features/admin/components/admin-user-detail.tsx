import { useCallback, useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Notice } from '@/components/ui/notice';
import { Select } from '@/components/ui/input';
import type { AdminUser } from '@/types/api';
import { getUser, updateUser } from '../api/admin-api';

export function AdminUserDetail() {
  const { id } = useParams<{ id: string }>();
  const [user, setUser] = useState<AdminUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // Editable fields
  const [role, setRole] = useState('');
  const [isActive, setIsActive] = useState(true);

  const load = useCallback(async () => {
    if (!id) return;
    setIsLoading(true);
    setError(null);
    try {
      const data = await getUser(Number(id));
      setUser(data);
      setRole(data.role);
      setIsActive(data.isActive);
    } catch {
      setError('Failed to load user.');
    } finally {
      setIsLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const handleSave = async () => {
    if (!user) return;
    setSaving(true);
    setError(null);
    setSuccess(null);
    try {
      const updated = await updateUser(user.id, { role, isActive });
      setUser(updated);
      setRole(updated.role);
      setIsActive(updated.isActive);
      setSuccess('User updated successfully.');
    } catch {
      setError('Failed to update user.');
    } finally {
      setSaving(false);
    }
  };

  const handleToggleActive = async () => {
    if (!user) return;
    setSaving(true);
    setError(null);
    setSuccess(null);
    try {
      const newActive = !user.isActive;
      const updated = await updateUser(user.id, { isActive: newActive });
      setUser(updated);
      setRole(updated.role);
      setIsActive(updated.isActive);
      setSuccess(newActive ? 'User reactivated.' : 'User deactivated.');
    } catch {
      setError('Failed to update user status.');
    } finally {
      setSaving(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
      </div>
    );
  }

  if (error && !user) {
    return (
      <div className="max-w-2xl mx-auto">
        <Notice variant="error" title={error} />
        <Link
          to="/admin/users"
          className="inline-flex items-center gap-1.5 text-sm text-brand-teal-500 hover:text-brand-teal-600 mt-4"
        >
          <ArrowLeft size={14} strokeWidth={1.8} />
          Back to users
        </Link>
      </div>
    );
  }

  if (!user) return null;

  return (
    <div className="max-w-2xl mx-auto">
      <Link
        to="/admin/users"
        className="inline-flex items-center gap-1.5 text-sm text-brand-teal-500 hover:text-brand-teal-600 mb-6"
      >
        <ArrowLeft size={14} strokeWidth={1.8} />
        Back to users
      </Link>

      <h1 className="text-2xl font-serif text-brand-slate-800 mb-6">
        {user.firstName} {user.lastName}
      </h1>

      {success && <div className="mb-4"><Notice variant="success" title={success} /></div>}
      {error && <div className="mb-4"><Notice variant="error" title={error} /></div>}

      <Card className="mb-6">
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

      <Card className="mb-6">
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
            <label className="text-[13px] font-medium text-brand-slate-600">Active</label>
            <button
              type="button"
              role="switch"
              aria-checked={isActive}
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
            <Button onClick={handleSave} disabled={saving} data-testid="admin-user-save">
              {saving ? 'Saving...' : 'Save Changes'}
            </Button>
          </div>
        </div>
      </Card>

      <Card>
        <h2 className="text-sm font-medium text-brand-slate-800 mb-3">Actions</h2>
        {user.isActive ? (
          <Button variant="danger" onClick={handleToggleActive} disabled={saving}>
            Deactivate User
          </Button>
        ) : (
          <Button variant="secondary" onClick={handleToggleActive} disabled={saving}>
            Reactivate User
          </Button>
        )}
      </Card>
    </div>
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
