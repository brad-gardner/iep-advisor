import { useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/use-auth';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useAuth();
  const successMessage = (location.state as { message?: string })?.message;
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    const result = await login({ email: email.trim(), password });

    if (result.success) {
      navigate('/dashboard');
    } else if (result.requiresMfa && result.mfaPendingToken) {
      navigate('/mfa-verify', { state: { mfaPendingToken: result.mfaPendingToken } });
    } else {
      setError(result.error || 'Login failed');
    }

    setIsLoading(false);
  };

  return (
    <div className="w-full">
      <h2 className="text-2xl font-serif font-semibold text-center mb-6 text-brand-slate-800">Welcome Back</h2>

      {successMessage && <div className="mb-4" data-testid="login-success-message"><Notice variant="success" title={successMessage} /></div>}
      {error && <div className="mb-4" data-testid="login-error"><Notice variant="error" title={error} /></div>}

      <form onSubmit={handleSubmit} className="space-y-4" data-testid="login-form">
        <Input
          label="Email"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
          placeholder="you@example.com"
          maxLength={256}
          data-testid="login-email"
        />

        <div>
          <Input
            label="Password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            placeholder="********"
            maxLength={128}
            data-testid="login-password"
          />
          <div className="mt-1 text-right">
            <Link
              to="/forgot-password"
              className="text-xs text-brand-teal-500 hover:text-brand-teal-600"
              data-testid="forgot-password-link"
            >
              Forgot password?
            </Link>
          </div>
        </div>

        <Button type="submit" disabled={isLoading} className="w-full" data-testid="login-submit">
          {isLoading ? 'Signing in...' : 'Sign In'}
        </Button>
      </form>

      <p className="mt-6 text-center text-sm text-brand-slate-400">
        Don't have an account?{' '}
        <Link to="/register" className="text-brand-teal-500 hover:text-brand-teal-600" data-testid="register-link">
          Sign up
        </Link>
      </p>
    </div>
  );
}
