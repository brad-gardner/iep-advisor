import { useState } from 'react';
import { useAuth } from '../hooks/use-auth';
import { StateSelector } from './state-selector';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';

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
    </div>
  );
}
