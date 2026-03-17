import { useState } from 'react';
import { Link } from 'react-router-dom';
import { forgotPassword } from '../api/auth-api';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

export function ForgotPasswordPage() {
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
          Check Your Email
        </h2>

        <Notice variant="success" title="If that email exists, we sent a reset link">
          <p className="mt-1">
            Please check your inbox and spam folder. The link will expire in 1 hour.
          </p>
        </Notice>

        <p className="mt-6 text-center text-sm text-brand-slate-400">
          <Link to="/login" className="text-brand-teal-500 hover:text-brand-teal-600">
            Back to login
          </Link>
        </p>
      </div>
    );
  }

  return (
    <div className="w-full">
      <h2 className="text-2xl font-serif font-semibold text-center mb-6 text-brand-slate-800">
        Reset Your Password
      </h2>

      <p className="text-sm text-brand-slate-400 text-center mb-6">
        Enter your email address and we'll send you a link to reset your password.
      </p>

      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Email"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
          placeholder="you@example.com"
          autoFocus
          maxLength={256}
          data-testid="forgot-email"
        />

        <Button type="submit" disabled={isLoading} className="w-full" data-testid="forgot-submit">
          {isLoading ? 'Sending...' : 'Send Reset Link'}
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
