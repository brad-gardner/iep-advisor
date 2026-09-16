import { formatDate } from '@/lib/format-date';
import { EmptyHint } from './empty-hint';
import { HomeSection } from './home-section';
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
    <HomeSection title={title} data-testid={testId}>
      {items.length === 0 ? (
        <EmptyHint data-testid={`${testId}-empty`}>{emptyHint}</EmptyHint>
      ) : (
        <ul className="divide-y divide-brand-slate-100">
          {items.map((item) => (
            <WorkItemRow
              key={item.instanceId}
              title={item.studentName}
              subtitle={formatDate(item[dateField])}
              href={`/educator/documents/${item.instanceId}`}
              data-testid={`${testId}-${item.instanceId}`}
            />
          ))}
        </ul>
      )}
    </HomeSection>
  );
}
