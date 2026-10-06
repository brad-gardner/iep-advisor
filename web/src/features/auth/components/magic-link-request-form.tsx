import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Notice } from '@/components/ui/notice';
import { requestMagicLink } from '../api/auth-api';

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
  const { t } = useTranslation('auth');
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
        <div role="status">
          <Notice variant="success" title={t('magicLink.confirmation')} />
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3" data-testid="magic-link-form">
      <Input
        label={t('fields.email')}
        type="email"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        required
        placeholder={t('fields.emailPlaceholder')}
        maxLength={256}
        data-testid="magic-link-email"
      />
      <Button type="submit" loading={isSubmitting} className="w-full" data-testid="magic-link-submit">
        {t('magicLink.submit')}
      </Button>
    </form>
  );
}
