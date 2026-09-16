import { formatDate } from '@/lib/format-date';
import { ListSection } from './list-section';
import { WorkItemRow } from './work-item-row';
import type { HomeProviderRequestDto } from '../types';

/** "Provider requests I owe" — plan 7 shape, always `[]` until that ships.
 * The Provider variant renders this section first (see `staff-home-body.tsx`). */
export function ProviderRequestsSection({ items }: { items: HomeProviderRequestDto[] }) {
  return (
    <ListSection
      title="Provider requests I owe"
      data-testid="home-provider-requests"
      items={items}
      emptyHint="No outstanding provider requests."
      itemKey={(item) => item.id}
      renderRow={(item) => (
        <WorkItemRow
          title={item.studentName}
          subtitle={`Due ${formatDate(item.dueDate)}`}
          href={`/educator/students/${item.studentId}`}
          data-testid={`home-provider-requests-${item.id}`}
        />
      )}
    />
  );
}
