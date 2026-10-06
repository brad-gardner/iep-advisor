import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../hooks/use-auth';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

interface ParentRegisterFormProps {
  // Invite code prefilled from the `?code=` URL param (beta invite links).
  initialInviteCode?: string;
}

export function ParentRegisterForm({ initialInviteCode = '' }: ParentRegisterFormProps) {
  const { t } = useTranslation('auth');
  const navigate = useNavigate();
  const { register } = useAuth();
  const [formData, setFormData] = useState({
    email: '',
    password: '',
    confirmPassword: '',
    firstName: '',
    lastName: '',
    inviteCode: initialInviteCode,
  });
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData((prev) => ({ ...prev, [e.target.name]: e.target.value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (formData.password !== formData.confirmPassword) {
      setError(t('errors.passwordsDoNotMatch'));
      return;
    }

    if (formData.password.length < 8) {
      setError(t('errors.passwordTooShort'));
      return;
    }

    setIsLoading(true);

    const result = await register({
      email: formData.email.trim(),
      password: formData.password,
      firstName: formData.firstName.trim(),
      lastName: formData.lastName.trim(),
      inviteCode: formData.inviteCode.trim(),
    });

    if (result.success) {
      navigate('/login', { state: { message: t('parentRegisterForm.successMessage') } });
    } else {
      setError(result.error || t('parentRegisterForm.failed'));
    }

    setIsLoading(false);
  };

  return (
    <>
      {error && (
        <div className="mb-4" data-testid="register-error">
          <Notice variant="error" title={error} />
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4" data-testid="register-form">
        <Input
          label={t('parentRegisterForm.inviteCode')}
          name="inviteCode"
          value={formData.inviteCode}
          onChange={handleChange}
          required
          placeholder={t('parentRegisterForm.inviteCodePlaceholder')}
          maxLength={20}
          data-testid="register-invite-code"
        />

        <div className="grid grid-cols-2 gap-4">
          <Input
            label={t('fields.firstName')}
            name="firstName"
            value={formData.firstName}
            onChange={handleChange}
            required
            maxLength={100}
            data-testid="register-first-name"
          />
          <Input
            label={t('fields.lastName')}
            name="lastName"
            value={formData.lastName}
            onChange={handleChange}
            required
            maxLength={100}
            data-testid="register-last-name"
          />
        </div>

        <Input
          label={t('fields.email')}
          name="email"
          type="email"
          value={formData.email}
          onChange={handleChange}
          required
          maxLength={256}
          data-testid="register-email"
        />

        <Input
          label={t('fields.password')}
          name="password"
          type="password"
          value={formData.password}
          onChange={handleChange}
          required
          maxLength={128}
          data-testid="register-password"
        />

        <Input
          label={t('fields.confirmPassword')}
          name="confirmPassword"
          type="password"
          value={formData.confirmPassword}
          onChange={handleChange}
          required
          maxLength={128}
          data-testid="register-confirm-password"
        />

        <Button type="submit" loading={isLoading} className="w-full" data-testid="register-submit">
          {t('parentRegisterForm.submit')}
        </Button>
      </form>
    </>
  );
}
