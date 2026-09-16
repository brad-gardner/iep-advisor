import { formatDate } from '@/lib/format-date';
import { ListSection } from './list-section';
import { WorkItemRow } from './work-item-row';
import type { HomeSharedDraftDto } from '../types';

interface SharedDraftListSectionProps {
  title: string;
  emptyHint: string;
  items: HomeSharedDraftDto[];
  /** Whether to show the response date (family responses) or the share date
   * (awaiting family) as the row's date context. */
  dateField: 'sharedAt' | 'respondedAt';
  'data-testid': string;
}

/** Generic list for the two plan-6 shared-draft sections ("Shared drafts
 * awaiting family" / "Family responses to review") — same DTO shape, always
 * `[]` until plan 6 ships, so this renders empty-safe by construction. */
export function SharedDraftListSection({
  title,
  emptyHint,
  items,
  dateField,
  'data-testid': testId,
}: SharedDraftListSectionProps) {
  return (
    <ListSection
      title={title}
      data-testid={testId}
      items={items}
      emptyHint={emptyHint}
      itemKey={(item) => item.instanceId}
      renderRow={(item) => (
        <WorkItemRow
          title={item.studentName}
          subtitle={formatDate(item[dateField])}
          href={`/educator/documents/${item.instanceId}`}
          data-testid={`${testId}-${item.instanceId}`}
        />
      )}
    />
  );
}
