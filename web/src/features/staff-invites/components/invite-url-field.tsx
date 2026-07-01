import { useState } from 'react';
import { Button } from '@/components/ui/button';

interface InviteUrlFieldProps {
  url: string;
}

// Dev-only affordance: the backend returns inviteUrl when ExposeLinksForTesting
// is on. Renders a read-only copyable link so a tester can hand it to the
// invitee without an email round-trip.
export function InviteUrlField({ url }: InviteUrlFieldProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };

  return (
    <div className="mt-2 space-y-1">
      <p className="text-xs text-brand-slate-500">Invite link (testing)</p>
      <div className="flex items-center gap-2">
        <input
          type="text"
          readOnly
          value={url}
          aria-label="Invite link"
          onFocus={(e) => e.target.select()}
          className="w-full px-3 py-2 bg-brand-slate-50 rounded-input text-brand-slate-600 text-xs border border-brand-slate-200"
          data-testid="staff-invite-url"
        />
        <Button
          variant="secondary"
          onClick={handleCopy}
          data-testid="staff-invite-url-copy"
        >
          {copied ? 'Copied' : 'Copy'}
        </Button>
      </div>
    </div>
  );
}
