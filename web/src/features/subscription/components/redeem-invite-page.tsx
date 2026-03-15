import { useState } from 'react';
import { Ticket } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { redeemInvite } from '../api/subscription-api';

export function RedeemInvitePage() {
  const [code, setCode] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [result, setResult] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim()) return;

    setIsSubmitting(true);
    setResult(null);

    try {
      const response = await redeemInvite(code.trim());
      if (response.success) {
        setResult({ type: 'success', text: 'Invite code redeemed! Your subscription is now active.' });
        setCode('');
      } else {
        setResult({ type: 'error', text: response.message || 'Invalid or expired invite code.' });
      }
    } catch {
      setResult({ type: 'error', text: 'Invalid or expired invite code.' });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="space-y-6">
      <h1 className="font-serif">Redeem Invite Code</h1>

      <Card className="max-w-md">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-10 h-10 rounded-full bg-brand-teal-50 flex items-center justify-center">
            <Ticket className="w-5 h-5 text-brand-teal-500" strokeWidth={1.8} aria-hidden="true" />
          </div>
          <p className="text-sm text-brand-slate-400">
            Enter the 8-character invite code you received to activate your subscription.
          </p>
        </div>

        {result && (
          <div className="mb-4">
            <Notice
              variant={result.type === 'success' ? 'success' : 'error'}
              title={result.text}
            />
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
          />
          <Button type="submit" disabled={isSubmitting || code.length < 8} className="w-full">
            {isSubmitting ? 'Redeeming...' : 'Redeem'}
          </Button>
        </form>
      </Card>
    </div>
  );
}
