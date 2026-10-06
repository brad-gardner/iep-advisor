import { useState } from 'react';
import { Link, useSearchParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { resetPassword } from '../api/auth-api';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { usePageTitle } from '@/hooks/use-page-title';

export function ResetPasswordPage() {
  const { t } = useTranslation('auth');
  usePageTitle(t('resetPassword.pageTitle'));
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';

  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  if (!token) {
    return (
      <div className="w-full">
        <h2 className="text-2xl font-serif font-semibold text-center mb-6 text-brand-slate-800">
          {t('resetPassword.invalidLinkTitle')}
        </h2>
        <Notice variant="error" title={t('resetPassword.invalidLink')} />
        <p className="mt-6 text-center text-sm text-brand-slate-500">
          <Link to="/forgot-password" className="text-brand-teal-500 hover:text-brand-teal-600">
            {t('resetPassword.requestNewLink')}
          </Link>
        </p>
      </div>
    );
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (newPassword.length < 8) {
      setError(t('errors.passwordTooShort'));
      return;
    }

    if (newPassword !== confirmPassword) {
      setError(t('errors.passwordsDoNotMatch'));
      return;
    }

    setIsLoading(true);
    try {
      const response = await resetPassword(token, newPassword);
      if (response.success) {
        navigate('/login', {
          state: { message: t('resetPassword.successMessage') },
        });
      } else {
        setError(response.message || t('resetPassword.failed'));
      }
    } catch {
      setError(t('errors.genericTryAgain'));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="w-full">
      <h2 className="text-2xl font-serif font-semibold text-center mb-6 text-brand-slate-800">
        {t('resetPassword.title')}
      </h2>

      {error && (
        <div className="mb-4">
          <Notice variant="error" title={error} />
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label={t('resetPassword.newPassword')}
          type="password"
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          required
          placeholder={t('fields.passwordPlaceholder')}
          minLength={8}
          maxLength={128}
          data-testid="reset-password"
        />

        <Input
          label={t('fields.confirmPassword')}
          type="password"
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
          required
          placeholder={t('fields.passwordPlaceholder')}
          minLength={8}
          maxLength={128}
          data-testid="reset-confirm-password"
        />

        <Button type="submit" loading={isLoading} className="w-full" data-testid="reset-submit">
          {t('resetPassword.submit')}
        </Button>
      </form>

      <p className="mt-6 text-center text-sm text-brand-slate-500">
        <Link to="/login" className="text-brand-teal-500 hover:text-brand-teal-600">
          {t('resetPassword.backToLogin')}
        </Link>
      </p>
    </div>
  );
}
