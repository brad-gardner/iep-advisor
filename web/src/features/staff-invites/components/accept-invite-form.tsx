import { useState } from 'react';
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
      setError('First and last name are required');
      return;
    }
    if (password.length < 8) {
      setError('Password must be at least 8 characters');
      return;
    }
    if (password !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    setIsSubmitting(true);
    const result = await onSubmit({
      firstName: firstName.trim(),
      lastName: lastName.trim(),
      password,
    });
    if (!result.success) {
      setError(result.error ?? 'Could not accept this invite');
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

      <Input label="Email" type="email" value={email} readOnly disabled data-testid="staff-accept-email" />

      <div className="grid grid-cols-2 gap-4">
        <Input
          label="First name"
          value={firstName}
          onChange={(e) => setFirstName(e.target.value)}
          required
          maxLength={100}
          data-testid="staff-accept-first-name"
        />
        <Input
          label="Last name"
          value={lastName}
          onChange={(e) => setLastName(e.target.value)}
          required
          maxLength={100}
          data-testid="staff-accept-last-name"
        />
      </div>

      <Input
        label="Password"
        type="password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        required
        minLength={8}
        maxLength={200}
        data-testid="staff-accept-password"
      />
      <Input
        label="Confirm password"
        type="password"
        value={confirmPassword}
        onChange={(e) => setConfirmPassword(e.target.value)}
        required
        minLength={8}
        maxLength={200}
        data-testid="staff-accept-confirm-password"
      />

      <Button type="submit" disabled={isSubmitting} className="w-full" data-testid="staff-accept-submit">
        {isSubmitting ? 'Creating account...' : 'Accept invite & create account'}
      </Button>
    </form>
  );
}
