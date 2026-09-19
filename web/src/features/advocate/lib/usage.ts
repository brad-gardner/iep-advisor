import type { AdvocateUsageDto } from '../types/advocate';

/** True once the yearly allowance is spent — the composer locks on this. */
export function isUsageCapped(usage: AdvocateUsageDto | null): boolean {
  return usage !== null && usage.limit > 0 && usage.used >= usage.limit;
}
