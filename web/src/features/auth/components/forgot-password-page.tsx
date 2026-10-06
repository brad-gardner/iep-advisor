import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { forgotPassword } from '../api/auth-api';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { usePageTitle } from '@/hooks/use-page-title';

export function ForgotPasswordPage() {
  const { t } = useTranslation('auth');
  usePageTitle(t('forgotPassword.pageTitle'));
  const [email, setEmail] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    try {
      await forgotPassword(email.trim());
    } catch {
      // Intentionally swallow — always show the same message
    } finally {
      setIsLoading(false);
      setSubmitted(true);
    }
  };

  if (submitted) {
    return (
      <div className="w-full">
        <h2 className="text-2xl font-serif font-semibold text-center mb-6 text-brand-slate-800">
          {t('forgotPassword.checkEmailTitle')}
        </h2>

        <Notice variant="success" title={t('forgotPassword.resetLinkSent')}>
          <p className="mt-1">
            {t('forgotPassword.resetLinkSentDetail')}
          </p>
        </Notice>

        <p className="mt-6 text-center text-sm text-brand-slate-500">
          <Link to="/login" className="text-brand-teal-500 hover:text-brand-teal-600">
            {t('forgotPassword.backToLogin')}
          </Link>
        </p>
      </div>
    );
  }

  return (
    <div className="w-full">
      <h2 className="text-2xl font-serif font-semibold text-center mb-6 text-brand-slate-800">
        {t('forgotPassword.title')}
      </h2>

      <p className="text-sm text-brand-slate-500 text-center mb-6">
        {t('forgotPassword.instructions')}
      </p>

      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label={t('fields.email')}
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
          placeholder={t('fields.emailPlaceholder')}
          autoFocus
          maxLength={256}
          data-testid="forgot-email"
        />

        <Button type="submit" loading={isLoading} className="w-full" data-testid="forgot-submit">
          {t('forgotPassword.submit')}
        </Button>
      </form>

      <p className="mt-6 text-center text-sm text-brand-slate-500">
        <Link to="/login" className="text-brand-teal-500 hover:text-brand-teal-600">
          {t('forgotPassword.backToLogin')}
        </Link>
      </p>
    </div>
  );
}
