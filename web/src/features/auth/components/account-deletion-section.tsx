import { useState } from 'react';
import { useAuth } from '../hooks/use-auth';
import { exportData, deleteAccount } from '../api/auth-api';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

export function AccountDeletionSection() {
  const { user, logout } = useAuth();
  const [showConfirm, setShowConfirm] = useState(false);
  const [password, setPassword] = useState('');
  const [mfaCode, setMfaCode] = useState('');
  const [error, setError] = useState('');
  const [isExporting, setIsExporting] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);

  const handleExport = async () => {
    setIsExporting(true);
    try {
      const response = await exportData();
      if (response.success && response.data) {
        const blob = new Blob([JSON.stringify(response.data, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `iep-assistant-data-export-${new Date().toISOString().slice(0, 10)}.json`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
      }
    } catch {
      // Silently handle export errors
    } finally {
      setIsExporting(false);
    }
  };

  const handleDelete = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsDeleting(true);

    try {
      const response = await deleteAccount(password, mfaCode || undefined);
      if (response.success) {
        logout();
      } else {
        setError(response.message || 'Failed to delete account');
      }
    } catch {
      setError('An error occurred. Please try again.');
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <div className="space-y-4">
      <div>
        <Button variant="secondary" onClick={handleExport} disabled={isExporting}>
          {isExporting ? 'Exporting...' : 'Export My Data'}
        </Button>
        <p className="text-xs text-brand-slate-300 mt-1">
          Download all your data as a JSON file
        </p>
      </div>

      {!showConfirm ? (
        <div>
          <Button variant="danger" onClick={() => setShowConfirm(true)}>
            Delete Account
          </Button>
        </div>
      ) : (
        <div className="border border-red-200 rounded-card p-4 bg-red-50">
          <Notice variant="warning" title="This action has a 30-day grace period">
            <p className="mt-1">
              Your account will be scheduled for deletion. You can cancel within 30 days by logging back in.
              After that, all data will be permanently removed.
            </p>
          </Notice>

          {error && (
            <div className="mt-3">
              <Notice variant="error" title={error} />
            </div>
          )}

          <form onSubmit={handleDelete} className="mt-4 space-y-3">
            <Input
              label="Confirm your password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              placeholder="Enter your password"
            />

            {user?.mfaEnabled && (
              <Input
                label="MFA Code"
                type="text"
                inputMode="numeric"
                value={mfaCode}
                onChange={(e) => setMfaCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                placeholder="000000"
                maxLength={6}
              />
            )}

            <div className="flex gap-3">
              <Button variant="danger" type="submit" disabled={isDeleting}>
                {isDeleting ? 'Deleting...' : 'Confirm Deletion'}
              </Button>
              <Button
                variant="ghost"
                type="button"
                onClick={() => {
                  setShowConfirm(false);
                  setPassword('');
                  setMfaCode('');
                  setError('');
                }}
              >
                Cancel
              </Button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
}
