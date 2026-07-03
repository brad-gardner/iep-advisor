import { useState } from 'react';
import { Ticket } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { useToast } from '@/components/ui/toast';
import { redeemInvite } from '../api/subscription-api';

export function RedeemInvitePage() {
  const { show } = useToast();
  const [code, setCode] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim()) return;

    setIsSubmitting(true);
    setError(null);

    try {
      const response = await redeemInvite(code.trim());
      if (response.success) {
        // Transient success → toast; inline space stays for decisions/errors.
        show({
          message: 'Invite code redeemed! Your subscription is now active.',
          variant: 'success',
        });
        setCode('');
      } else {
        setError(response.message || 'Invalid or expired invite code.');
      }
    } catch {
      setError('Invalid or expired invite code.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <PageLayout title="Redeem Invite Code">
      <Card className="max-w-md">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-10 h-10 rounded-full bg-brand-teal-50 flex items-center justify-center">
            <Ticket className="w-5 h-5 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
          </div>
          <p className="text-sm text-brand-slate-400">
            Enter the 8-character invite code you received to activate your subscription.
          </p>
        </div>

        {error && (
          <div className="mb-4">
            <Notice variant="error" title={error} />
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <Input
            label="Invite Code"
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase().slice(0, 8))}
            placeholder="ABCD1234"
            maxLength={8}
            required
            data-testid="redeem-code"
          />
          <Button
            type="submit"
            disabled={code.length < 8}
            loading={isSubmitting}
            className="w-full"
            data-testid="redeem-submit"
          >
            Redeem
          </Button>
        </form>
      </Card>
    </PageLayout>
  );
}
