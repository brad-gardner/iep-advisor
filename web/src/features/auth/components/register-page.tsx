import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/use-auth';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

export function RegisterPage() {
  const navigate = useNavigate();
  const { register } = useAuth();
  const [formData, setFormData] = useState({
    email: '',
    password: '',
    confirmPassword: '',
    firstName: '',
    lastName: '',
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
      setError('Passwords do not match');
      return;
    }

    if (formData.password.length < 8) {
      setError('Password must be at least 8 characters');
      return;
    }

    setIsLoading(true);

    const result = await register({
      email: formData.email,
      password: formData.password,
      firstName: formData.firstName,
      lastName: formData.lastName,
    });

    if (result.success) {
      navigate('/login', { state: { message: 'Registration successful! Please sign in.' } });
    } else {
      setError(result.error || 'Registration failed');
    }

    setIsLoading(false);
  };

  return (
    <div className="w-full">
      <h2 className="text-2xl font-serif font-semibold text-center mb-6 text-brand-slate-800">Create Your Account</h2>

      {error && <div className="mb-4"><Notice variant="error" title={error} /></div>}

      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <Input
            label="First Name"
            name="firstName"
            value={formData.firstName}
            onChange={handleChange}
            required
          />
          <Input
            label="Last Name"
            name="lastName"
            value={formData.lastName}
            onChange={handleChange}
            required
          />
        </div>

        <Input
          label="Email"
          name="email"
          type="email"
          value={formData.email}
          onChange={handleChange}
          required
        />

        <Input
          label="Password"
          name="password"
          type="password"
          value={formData.password}
          onChange={handleChange}
          required
        />

        <Input
          label="Confirm Password"
          name="confirmPassword"
          type="password"
          value={formData.confirmPassword}
          onChange={handleChange}
          required
        />

        <Button type="submit" disabled={isLoading} className="w-full">
          {isLoading ? 'Creating account...' : 'Create Account'}
        </Button>
      </form>

      <p className="mt-6 text-center text-sm text-brand-slate-400">
        Already have an account?{' '}
        <Link to="/login" className="text-brand-teal-500 hover:text-brand-teal-600">
          Sign in
        </Link>
      </p>
    </div>
  );
}
