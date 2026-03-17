import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { createCheckoutSession } from '../api/subscription-api';

export function SubscribeButton() {
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
      setIsLoading(false);
    }
  };

  return (
    <Button onClick={handleSubscribe} disabled={isLoading} data-testid="subscribe-button">
      {isLoading ? 'Redirecting...' : 'Subscribe \u2014 $50/year'}
    </Button>
  );
}
