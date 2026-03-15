import { apiClient } from '@/lib/api-client';
import type { ApiResponse, SubscriptionStatus } from '@/types/api';

export async function createCheckoutSession(
  successUrl: string,
  cancelUrl: string,
): Promise<{ url: string }> {
  const response = await apiClient.post<{ url: string }>(
    '/api/stripe/create-checkout-session',
    { successUrl, cancelUrl },
  );
  return response.data;
}

export async function createPortalSession(
  returnUrl: string,
): Promise<{ url: string }> {
  const response = await apiClient.post<{ url: string }>(
    '/api/stripe/create-portal-session',
    { returnUrl },
  );
  return response.data;
}

export async function getSubscriptionStatus(): Promise<SubscriptionStatus> {
  const response = await apiClient.get<SubscriptionStatus>(
    '/api/subscription/status',
  );
  return response.data;
}

export async function redeemInvite(
  code: string,
): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>(
    '/api/subscription/redeem-invite',
    { code },
  );
  return response.data;
}
