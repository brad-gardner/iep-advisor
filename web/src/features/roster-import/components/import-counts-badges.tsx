import { Badge } from '@/components/ui/badge';
import type { ImportCounts } from '../types';

interface ImportCountsBadgesProps {
  counts: ImportCounts;
  testId?: string;
}

// Outcome tallies. Each badge carries its own text label so the meaning never
// rides on colour alone.
export function ImportCountsBadges({ counts, testId = 'import-counts' }: ImportCountsBadgesProps) {
  return (
    <ul className="flex flex-wrap gap-2" aria-label="Import summary" data-testid={testId}>
      <li>
        <Badge variant="success" data-testid={`${testId}-new`}>
          {counts.new} new
        </Badge>
      </li>
      <li>
        <Badge variant="info" data-testid={`${testId}-updated`}>
          {counts.updated} updated
        </Badge>
      </li>
      <li>
        <Badge variant="neutral" data-testid={`${testId}-unchanged`}>
          {counts.unchanged} unchanged
        </Badge>
      </li>
      <li>
        <Badge variant={counts.error > 0 ? 'error' : 'neutral'} data-testid={`${testId}-error`}>
          {counts.error} {counts.error === 1 ? 'error' : 'errors'}
        </Badge>
      </li>
    </ul>
  );
}
