import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { requestMagicLink } from '../api/auth-api';

const CONFIRMATION_MESSAGE = 'If that address is eligible, a link is on its way.';

/**
 * The small "email me a sign-in link" form (pilot-gates plan, phase 3):
 * staff (RelatedServiceProvider/GeneralEducator) can sign in via a 15-minute
 * emailed link instead of a password. Reused by the login page (as a toggle)
 * and by the magic-link consume page's "invalid or expired" failure state.
 *
 * Always shows the same confirmation once submitted — success, ineligible
 * email, and even a transport failure render identically — so the response
 * never reveals whether a given address exists or is eligible.
 */
export function MagicLinkRequestForm() {
  const [email, setEmail] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    try {
      await requestMagicLink(email.trim());
    } catch {
      // Intentionally swallowed — see the no-enumeration note above.
    } finally {
      setIsSubmitting(false);
      setSubmitted(true);
    }
  };

  if (submitted) {
    return (
      <div data-testid="magic-link-message">
        <Notice variant="success" title={CONFIRMATION_MESSAGE} />
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3" data-testid="magic-link-form">
      <Input
        label="Email"
        type="email"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        required
        placeholder="you@example.com"
        maxLength={256}
        data-testid="magic-link-email"
      />
      <Button type="submit" loading={isSubmitting} className="w-full" data-testid="magic-link-submit">
        Send sign-in link
      </Button>
    </form>
  );
}
