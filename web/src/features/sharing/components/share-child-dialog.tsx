import { useState } from 'react';
import { Send } from 'lucide-react';
import { createInvite } from '../api/sharing-api';
import { Button } from '@/components/ui/button';
import { Input, Select } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';

interface ShareChildDialogProps {
  childId: number;
  onInvited: () => void;
  onCancel: () => void;
}

export function ShareChildDialog({ childId, onInvited, onCancel }: ShareChildDialogProps) {
  const [email, setEmail] = useState('');
  const [role, setRole] = useState('viewer');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim()) return;

    setIsSubmitting(true);
    setError(null);
    setSuccess(false);

    try {
      const response = await createInvite(childId, { email: email.trim(), role });
      if (response.success) {
        setSuccess(true);
        setEmail('');
        onInvited();
      } else {
        setError(response.message || 'Failed to send invite');
      }
    } catch {
      setError('An error occurred while sending the invite');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="bg-brand-slate-50 rounded-card p-4 border border-brand-slate-200">
      <h3 className="font-serif text-brand-slate-800 mb-3">Share Access</h3>

      {error && (
        <div className="mb-3">
          <Notice variant="error" title={error} />
        </div>
      )}

      {success && (
        <div className="mb-3">
          <Notice variant="success" title="Invite sent successfully" />
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-3">
        <Input
          label="Email Address"
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="parent@example.com"
          required
          maxLength={256}
        />

        <Select
          label="Role"
          value={role}
          onChange={(e) => setRole(e.target.value)}
        >
          <option value="viewer">Viewer</option>
          <option value="collaborator">Collaborator</option>
        </Select>

        <div className="flex gap-2 pt-1">
          <Button type="submit" disabled={isSubmitting || !email.trim()}>
            <Send className="w-4 h-4 mr-1.5" strokeWidth={1.8} aria-hidden="true" />
            {isSubmitting ? 'Sending...' : 'Send Invite'}
          </Button>
          <Button variant="ghost" type="button" onClick={onCancel}>
            Cancel
          </Button>
        </div>
      </form>
    </div>
  );
}
