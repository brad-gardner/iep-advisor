import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

interface AcceptInviteFormProps {
  // Email is bound to the invite and shown read-only.
  email: string;
  onSubmit: (data: {
    firstName: string;
    lastName: string;
    password: string;
  }) => Promise<{ success: boolean; error?: string }>;
}

export function AcceptInviteForm({ email, onSubmit }: AcceptInviteFormProps) {
  const { t } = useTranslation('auth');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!firstName.trim() || !lastName.trim()) {
      setError(t('acceptInviteForm.namesRequired'));
      return;
    }
    if (password.length < 8) {
      setError(t('acceptInviteForm.passwordTooShort'));
      return;
    }
    if (password !== confirmPassword) {
      setError(t('acceptInviteForm.passwordsDoNotMatch'));
      return;
    }

    setIsSubmitting(true);
    const result = await onSubmit({
      firstName: firstName.trim(),
      lastName: lastName.trim(),
      password,
    });
    if (!result.success) {
      setError(result.error ?? t('acceptInviteForm.failed'));
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4 text-left" data-testid="staff-accept-form">
      {error && (
        <div data-testid="staff-accept-error">
          <Notice variant="error" title={error} />
        </div>
      )}

      <Input label={t('fields.email')} type="email" value={email} readOnly disabled data-testid="staff-accept-email" />

      <div className="grid grid-cols-2 gap-4">
        <Input
          label={t('acceptInviteForm.firstName')}
          value={firstName}
          onChange={(e) => setFirstName(e.target.value)}
          required
          maxLength={100}
          data-testid="staff-accept-first-name"
        />
        <Input
          label={t('acceptInviteForm.lastName')}
          value={lastName}
          onChange={(e) => setLastName(e.target.value)}
          required
          maxLength={100}
          data-testid="staff-accept-last-name"
        />
      </div>

      <Input
        label={t('acceptInviteForm.password')}
        type="password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        required
        minLength={8}
        maxLength={200}
        data-testid="staff-accept-password"
      />
      <Input
        label={t('acceptInviteForm.confirmPassword')}
        type="password"
        value={confirmPassword}
        onChange={(e) => setConfirmPassword(e.target.value)}
        required
        minLength={8}
        maxLength={200}
        data-testid="staff-accept-confirm-password"
      />

      <Button type="submit" disabled={isSubmitting} className="w-full" data-testid="staff-accept-submit">
        {isSubmitting ? t('acceptInviteForm.submitting') : t('acceptInviteForm.submit')}
      </Button>
    </form>
  );
}
