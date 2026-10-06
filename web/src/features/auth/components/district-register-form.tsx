import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../hooks/use-auth';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

export function DistrictRegisterForm() {
  const { t } = useTranslation('auth');
  const navigate = useNavigate();
  const { registerDistrict } = useAuth();
  const [formData, setFormData] = useState({
    email: '',
    password: '',
    confirmPassword: '',
    firstName: '',
    lastName: '',
    districtName: '',
    stateCode: '',
  });
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData((prev) => ({ ...prev, [e.target.name]: e.target.value }));
  };

  // State code is an optional 2-letter code; force uppercase as the user types.
  const handleStateCodeChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData((prev) => ({ ...prev, stateCode: e.target.value.toUpperCase() }));
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

    const stateCode = formData.stateCode.trim();
    if (stateCode && stateCode.length !== 2) {
      setError(t('districtRegisterForm.stateLength'));
      return;
    }

    setIsLoading(true);

    const result = await registerDistrict({
      email: formData.email.trim(),
      password: formData.password,
      firstName: formData.firstName.trim(),
      lastName: formData.lastName.trim(),
      districtName: formData.districtName.trim(),
      stateCode: stateCode || undefined,
    });

    if (result.success) {
      // New districts land in the first-run setup wizard. The session is now
      // persisted, so the enclosing PublicRoute re-renders and would otherwise
      // bounce us to the role's default home (/educator), skipping the wizard.
      // Record the intended post-auth destination so PublicRoute honors it
      // regardless of which navigation React Router commits last.
      sessionStorage.setItem('post-auth-redirect', '/educator/setup');
      navigate('/educator/setup', { replace: true });
    } else {
      setError(result.error || t('districtRegisterForm.failed'));
      setIsLoading(false);
    }
  };

  return (
    <>
      {error && (
        <div className="mb-4" data-testid="register-district-error">
          <Notice variant="error" title={error} />
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4" data-testid="register-district-form">
        <div className="grid grid-cols-2 gap-4">
          <Input
            label={t('fields.firstName')}
            name="firstName"
            value={formData.firstName}
            onChange={handleChange}
            required
            maxLength={100}
            data-testid="register-district-first-name"
          />
          <Input
            label={t('fields.lastName')}
            name="lastName"
            value={formData.lastName}
            onChange={handleChange}
            required
            maxLength={100}
            data-testid="register-district-last-name"
          />
        </div>

        <Input
          label={t('districtRegisterForm.workEmail')}
          name="email"
          type="email"
          value={formData.email}
          onChange={handleChange}
          required
          maxLength={256}
          data-testid="register-district-email"
        />

        <Input
          label={t('districtRegisterForm.districtName')}
          name="districtName"
          value={formData.districtName}
          onChange={handleChange}
          required
          maxLength={200}
          placeholder={t('districtRegisterForm.districtNamePlaceholder')}
          data-testid="register-district-name"
        />

        <Input
          label={t('districtRegisterForm.stateOptional')}
          name="stateCode"
          value={formData.stateCode}
          onChange={handleStateCodeChange}
          maxLength={2}
          placeholder={t('districtRegisterForm.statePlaceholder')}
          autoCapitalize="characters"
          data-testid="register-district-state"
        />

        <Input
          label={t('fields.password')}
          name="password"
          type="password"
          value={formData.password}
          onChange={handleChange}
          required
          maxLength={128}
          data-testid="register-district-password"
        />

        <Input
          label={t('fields.confirmPassword')}
          name="confirmPassword"
          type="password"
          value={formData.confirmPassword}
          onChange={handleChange}
          required
          maxLength={128}
          data-testid="register-district-confirm-password"
        />

        <Button
          type="submit"
          loading={isLoading}
          className="w-full"
          data-testid="register-district-submit"
        >
          {t('districtRegisterForm.submit')}
        </Button>
      </form>
    </>
  );
}
