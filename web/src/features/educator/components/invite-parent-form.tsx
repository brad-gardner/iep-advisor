import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

interface InviteParentFormProps {
  onInvite: (email: string) => Promise<{ success: boolean; message?: string }>;
}

export function InviteParentForm({ onInvite }: InviteParentFormProps) {
  const [email, setEmail] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);
    setSuccessMessage(null);

    const result = await onInvite(email.trim());

    if (result.success) {
      setSuccessMessage(result.message || 'Invitation sent. The parent has a pending invite.');
      setEmail('');
    } else {
      setError(result.message ?? 'Failed to send invitation');
    }

    setIsSubmitting(false);
  };

  return (
    <Card className="max-w-lg">
      <h2 className="font-serif text-lg mb-4">Invite a parent</h2>
      <form onSubmit={handleSubmit} className="space-y-4" data-testid="invite-parent-form">
        {error && <Notice variant="error" title={error} />}
        {successMessage && <Notice variant="success" title="Invitation sent">{successMessage}</Notice>}

        <Input
          label="Parent Email *"
          type="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          maxLength={256}
          data-testid="invite-parent-email"
        />

        <Button
          type="submit"
          disabled={isSubmitting}
          className="w-full"
          data-testid="invite-parent-submit"
        >
          {isSubmitting ? 'Sending...' : 'Send Invite'}
        </Button>
      </form>
    </Card>
  );
}
