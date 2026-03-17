import { useState } from 'react';
import { Link, useSearchParams, useNavigate } from 'react-router-dom';
import { resetPassword } from '../api/auth-api';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

export function ResetPasswordPage() {
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
          Invalid Reset Link
        </h2>
        <Notice variant="error" title="This reset link is invalid or has expired" />
        <p className="mt-6 text-center text-sm text-brand-slate-400">
          <Link to="/forgot-password" className="text-brand-teal-500 hover:text-brand-teal-600">
            Request a new reset link
          </Link>
        </p>
      </div>
    );
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (newPassword.length < 8) {
      setError('Password must be at least 8 characters');
      return;
    }

    if (newPassword !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    setIsLoading(true);
    try {
      const response = await resetPassword(token, newPassword);
      if (response.success) {
        navigate('/login', {
          state: { message: 'Password reset successful. Please sign in with your new password.' },
        });
      } else {
        setError(response.message || 'Failed to reset password. The link may have expired.');
      }
    } catch {
      setError('An error occurred. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="w-full">
      <h2 className="text-2xl font-serif font-semibold text-center mb-6 text-brand-slate-800">
        Set New Password
      </h2>

      {error && (
        <div className="mb-4">
          <Notice variant="error" title={error} />
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="New Password"
          type="password"
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          required
          placeholder="********"
          minLength={8}
          maxLength={128}
          data-testid="reset-password"
        />

        <Input
          label="Confirm Password"
          type="password"
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
          required
          placeholder="********"
          minLength={8}
          maxLength={128}
          data-testid="reset-confirm-password"
        />

        <Button type="submit" disabled={isLoading} className="w-full" data-testid="reset-submit">
          {isLoading ? 'Resetting...' : 'Reset Password'}
        </Button>
      </form>

      <p className="mt-6 text-center text-sm text-brand-slate-400">
        <Link to="/login" className="text-brand-teal-500 hover:text-brand-teal-600">
          Back to login
        </Link>
      </p>
    </div>
  );
}
