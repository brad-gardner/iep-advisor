import type { SharedDraftStatus } from '../types';

/** Badge tone per revision status (status is never conveyed by color alone —
 *  every usage pairs this with the status text itself). */
export const SHARED_DRAFT_STATUS_BADGE: Record<SharedDraftStatus, 'success' | 'error' | 'neutral'> = {
  Active: 'success',
  Withdrawn: 'error',
  Superseded: 'neutral',
};
