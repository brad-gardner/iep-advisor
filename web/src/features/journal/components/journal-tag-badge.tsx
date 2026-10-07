import { Badge } from '@/components/ui/badge';
import { journalTagLabel } from '../lib/tag-label';
import type { JournalTag } from '../types/journal';

const VARIANT: Record<JournalTag, 'success' | 'warning' | 'error' | 'info' | 'neutral'> = {
  Incident: 'warning',
  Communication: 'info',
  Medical: 'error',
  Progress: 'success',
  Other: 'neutral',
};

export function JournalTagBadge({ tag, 'data-testid': testId }: { tag: JournalTag; 'data-testid'?: string }) {
  return (
    <Badge variant={VARIANT[tag] ?? 'neutral'} data-testid={testId}>
      {journalTagLabel(tag)}
    </Badge>
  );
}
