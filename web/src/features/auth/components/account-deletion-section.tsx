import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../hooks/use-auth';
import { exportData, deleteAccount } from '../api/auth-api';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

export function AccountDeletionSection() {
  const { t } = useTranslation('auth');
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
        setError(response.message || t('accountDeletion.failed'));
      }
    } catch {
      setError(t('accountDeletion.error'));
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <div className="space-y-4">
      <div>
        <Button variant="secondary" onClick={handleExport} loading={isExporting} data-testid="export-data">
          {t('accountDeletion.exportButton')}
        </Button>
        <p className="text-xs text-brand-slate-500 mt-1">
          {t('accountDeletion.exportHint')}
        </p>
      </div>

      {!showConfirm ? (
        <div>
          <Button variant="danger" onClick={() => setShowConfirm(true)} data-testid="delete-account">
            {t('accountDeletion.deleteButton')}
          </Button>
        </div>
      ) : (
        <div className="border border-brand-danger-200 rounded-card p-4 bg-brand-danger-50">
          <Notice variant="warning" title={t('accountDeletion.gracePeriodTitle')}>
            <p className="mt-1">
              {t('accountDeletion.gracePeriodDetail')}
            </p>
          </Notice>

          {error && (
            <div className="mt-3">
              <Notice variant="error" title={error} />
            </div>
          )}

          <form onSubmit={handleDelete} className="mt-4 space-y-3">
            <Input
              label={t('accountDeletion.confirmPasswordLabel')}
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              placeholder={t('accountDeletion.passwordPlaceholder')}
              data-testid="delete-account-password"
            />

            {user?.mfaEnabled && (
              <Input
                label={t('accountDeletion.mfaCode')}
                type="text"
                inputMode="numeric"
                value={mfaCode}
                onChange={(e) => setMfaCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                placeholder={t('fields.codePlaceholder')}
                maxLength={6}
              />
            )}

            <div className="flex gap-3">
              <Button variant="danger" type="submit" loading={isDeleting} data-testid="confirm-delete-account">
                {t('accountDeletion.confirmButton')}
              </Button>
              <Button
                variant="ghost"
                type="button"
                data-testid="cancel-delete-account"
                onClick={() => {
                  setShowConfirm(false);
                  setPassword('');
                  setMfaCode('');
                  setError('');
                }}
              >
                {t('accountDeletion.cancelButton')}
              </Button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
}
