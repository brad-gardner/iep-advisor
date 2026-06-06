import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';

interface InviteStudentFormProps {
  // Parameterized so the same form serves both the parent and educator flows.
  onInvite: (email: string) => Promise<{ success: boolean; message?: string }>;
  description?: string;
}

export function InviteStudentForm({ onInvite, description }: InviteStudentFormProps) {
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
      setSuccessMessage(result.message || 'Invitation sent. The student has a pending invite.');
      setEmail('');
    } else {
      setError(result.message ?? 'Failed to send invitation');
    }

    setIsSubmitting(false);
  };

  return (
    <Card className="max-w-lg">
      <h2 className="font-serif text-lg mb-2">Invite student</h2>
      <p className="text-sm text-brand-slate-400 mb-4">
        {description ??
          'Invite the student to activate their own account and participate in their IEP process.'}
      </p>
      <form onSubmit={handleSubmit} className="space-y-4" data-testid="invite-student-form">
        {error && <Notice variant="error" title={error} />}
        {successMessage && (
          <Notice variant="success" title="Invitation sent">
            {successMessage}
          </Notice>
        )}

        <Input
          label="Student Email *"
          type="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          maxLength={256}
          data-testid="invite-student-email"
        />

        <Button
          type="submit"
          disabled={isSubmitting}
          className="w-full"
          data-testid="invite-student-submit"
        >
          {isSubmitting ? 'Sending...' : 'Send Invite'}
        </Button>
      </form>
    </Card>
  );
}
