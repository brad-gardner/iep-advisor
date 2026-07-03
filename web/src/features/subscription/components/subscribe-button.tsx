import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { createCheckoutSession } from '../api/subscription-api';

export function SubscribeButton() {
  const { show } = useToast();
  const [isLoading, setIsLoading] = useState(false);

  const handleSubscribe = async () => {
    setIsLoading(true);
    try {
      const currentUrl = window.location.origin;
      const { url } = await createCheckoutSession(
        `${currentUrl}/subscription/success`,
        `${currentUrl}/subscription/cancel`,
      );
      window.location.href = url;
    } catch {
      // Redirect failed — surface it instead of silently resetting.
      show({ message: 'Could not start checkout. Please try again.', variant: 'error' });
      setIsLoading(false);
    }
  };

  return (
    <Button onClick={handleSubscribe} loading={isLoading} data-testid="subscribe-button">
      Subscribe &mdash; $50/year
    </Button>
  );
}
