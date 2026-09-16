import { Button } from '@/components/ui/button';
import type { InviteStatus } from '../types';

interface RsvpButtonGroupProps {
  onRespond: (status: InviteStatus) => void;
  /** The status currently in flight, or `null` when idle. All three buttons
   * disable together while any one is pending. */
  pending: InviteStatus | null;
  /** Prefix for each button's `data-testid` (`${prefix}-accept` etc). */
  testIdPrefix: string;
}

/** Accept / Tentative / Decline RSVP buttons, shared by every "next meeting"
 * card (parent, student, home). */
export function RsvpButtonGroup({ onRespond, pending, testIdPrefix }: RsvpButtonGroupProps) {
  return (
    <div className="mt-3 flex flex-wrap gap-2">
      <Button
        size="sm"
        onClick={() => onRespond('Accepted')}
        loading={pending === 'Accepted'}
        disabled={pending !== null}
        data-testid={`${testIdPrefix}-accept`}
      >
        Accept
      </Button>
      <Button
        size="sm"
        variant="secondary"
        onClick={() => onRespond('Tentative')}
        loading={pending === 'Tentative'}
        disabled={pending !== null}
        data-testid={`${testIdPrefix}-tentative`}
      >
        Tentative
      </Button>
      <Button
        size="sm"
        variant="danger"
        onClick={() => onRespond('Declined')}
        loading={pending === 'Declined'}
        disabled={pending !== null}
        data-testid={`${testIdPrefix}-decline`}
      >
        Decline
      </Button>
    </div>
  );
}
