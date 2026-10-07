import { useTranslation } from 'react-i18next';
import { Badge } from '@/components/ui/badge';
import type { ImportCounts } from '../types';

interface ImportCountsBadgesProps {
  counts: ImportCounts;
  testId?: string;
}

// Outcome tallies. Each badge carries its own text label so the meaning never
// rides on colour alone.
export function ImportCountsBadges({ counts, testId = 'import-counts' }: ImportCountsBadgesProps) {
  const { t } = useTranslation('roster-import');
  return (
    <ul className="flex flex-wrap gap-2" aria-label={t('countsBadges.ariaLabel')} data-testid={testId}>
      <li>
        <Badge variant="success" data-testid={`${testId}-new`}>
          {t('countsBadges.new', { count: counts.new })}
        </Badge>
      </li>
      <li>
        <Badge variant="info" data-testid={`${testId}-updated`}>
          {t('countsBadges.updated', { count: counts.updated })}
        </Badge>
      </li>
      <li>
        <Badge variant="neutral" data-testid={`${testId}-unchanged`}>
          {t('countsBadges.unchanged', { count: counts.unchanged })}
        </Badge>
      </li>
      <li>
        <Badge variant={counts.error > 0 ? 'error' : 'neutral'} data-testid={`${testId}-error`}>
          {t('countsBadges.errors', { count: counts.error })}
        </Badge>
      </li>
    </ul>
  );
}
