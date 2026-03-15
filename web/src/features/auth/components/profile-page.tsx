import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../hooks/use-auth';
import { disableMfa } from '../api/auth-api';
import { StateSelector } from './state-selector';
import { AccountDeletionSection } from './account-deletion-section';
import { SubscriptionStatusCard } from '@/features/subscription/components/subscription-status';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Notice } from '@/components/ui/notice';

function MfaSection() {
  const { user } = useAuth();
  const [showDisable, setShowDisable] = useState(false);
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [disableSuccess, setDisableSuccess] = useState(false);

  const handleDisable = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);
    try {
      const response = await disableMfa(password, code.trim());
      if (response.success) {
        setDisableSuccess(true);
        setShowDisable(false);
      } else {
        setError(response.message || 'Failed to disable MFA');
      }
    } catch {
      setError('An error occurred');
    } finally {
      setIsLoading(false);
    }
  };

  const isMfaEnabled = user?.mfaEnabled && !disableSuccess;

  return (
    <div className="space-y-3">
      {disableSuccess && (
        <Notice variant="success" title="MFA has been disabled" />
      )}

      {isMfaEnabled ? (
        <>
          <div className="flex items-center gap-2">
            <Badge variant="success">MFA Enabled</Badge>
          </div>

          {!showDisable ? (
            <Button variant="ghost" onClick={() => setShowDisable(true)}>
              Disable MFA
            </Button>
          ) : (
            <div className="border border-brand-slate-100 rounded-card p-4">
              {error && (
                <div className="mb-3">
                  <Notice variant="error" title={error} />
                </div>
              )}
              <form onSubmit={handleDisable} className="space-y-3">
                <Input
                  label="Password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  placeholder="Enter your password"
                />
                <Input
                  label="Authenticator Code"
                  type="text"
                  inputMode="numeric"
                  value={code}
                  onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  required
                  placeholder="000000"
                  maxLength={6}
                />
                <div className="flex gap-3">
                  <Button type="submit" disabled={isLoading}>
                    {isLoading ? 'Disabling...' : 'Confirm Disable'}
                  </Button>
                  <Button
                    variant="ghost"
                    type="button"
                    onClick={() => {
                      setShowDisable(false);
                      setPassword('');
                      setCode('');
                      setError('');
                    }}
                  >
                    Cancel
                  </Button>
                </div>
              </form>
            </div>
          )}
        </>
      ) : (
        <div>
          <p className="text-sm text-brand-slate-400 mb-2">
            Add an extra layer of security with two-factor authentication.
          </p>
          <Link to="/mfa-setup">
            <Button variant="secondary">Enable MFA</Button>
          </Link>
        </div>
      )}
    </div>
  );
}

export function ProfilePage() {
  const { user, updateProfile } = useAuth();
  const [firstName, setFirstName] = useState(user?.firstName ?? '');
  const [lastName, setLastName] = useState(user?.lastName ?? '');
  const [state, setState] = useState(user?.state ?? '');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setMessage(null);

    const result = await updateProfile({
      firstName,
      lastName,
      state: state || undefined,
    });

    if (result.success) {
      setMessage({ type: 'success', text: 'Profile updated successfully' });
    } else {
      setMessage({ type: 'error', text: result.error ?? 'Update failed' });
    }

    setIsSubmitting(false);
  };

  return (
    <div className="space-y-6">
      <h1 className="font-serif">Your Profile</h1>

      <Card className="max-w-lg">
        <form onSubmit={handleSubmit} className="space-y-4">
          {message && (
            <Notice
              variant={message.type === 'success' ? 'success' : 'error'}
              title={message.text}
            />
          )}

          <Input
            label="Email"
            type="text"
            value={user?.email ?? ''}
            disabled
            className="bg-brand-slate-50 text-brand-slate-400 cursor-not-allowed"
          />

          <Input
            label="First Name"
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
          />

          <Input
            label="Last Name"
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
          />

          <div>
            <label htmlFor="state" className="block text-[13px] font-medium text-brand-slate-600 mb-1">
              State / Jurisdiction
            </label>
            <StateSelector value={state} onChange={setState} />
            <p className="text-[11px] text-brand-slate-300 mt-1">
              Used to provide state-specific IEP guidance and regulations
            </p>
          </div>

          <Button type="submit" disabled={isSubmitting} className="w-full">
            {isSubmitting ? 'Saving...' : 'Save Changes'}
          </Button>
        </form>
      </Card>

      <div className="max-w-lg space-y-3">
        <SubscriptionStatusCard />
        <Link
          to="/redeem-invite"
          className="inline-block text-sm text-brand-teal-500 hover:text-brand-teal-600 underline"
        >
          Redeem Invite Code
        </Link>
      </div>

      <Card className="max-w-lg">
        <h2 className="text-lg font-serif font-semibold text-brand-slate-800 mb-4">
          Two-Factor Authentication
        </h2>
        <MfaSection />
      </Card>

      <Card className="max-w-lg">
        <h2 className="text-lg font-serif font-semibold text-brand-slate-800 mb-4">
          Account
        </h2>
        <AccountDeletionSection />
      </Card>
    </div>
  );
}
