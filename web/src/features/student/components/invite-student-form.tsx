import { useState } from 'react';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { useToast } from '@/components/ui/toast';

interface InviteStudentFormProps {
  // Parameterized so the same form serves both the parent and educator flows.
  onInvite: (email: string) => Promise<{ success: boolean; message?: string }>;
  description?: string;
}

export function InviteStudentForm({ onInvite, description }: InviteStudentFormProps) {
  const { show } = useToast();
  const [email, setEmail] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);

    const result = await onInvite(email.trim());

    if (result.success) {
      // Transient success → toast; the form resets for the next invite.
      show({
        message: result.message || 'Invitation sent. The student has a pending invite.',
        variant: 'success',
      });
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

        <Input
          id="invite-student-email"
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
          loading={isSubmitting}
          className="w-full"
          data-testid="invite-student-submit"
        >
          Send Invite
        </Button>
      </form>
    </Card>
  );
}
