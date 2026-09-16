import { formatDate } from '@/lib/format-date';
import { EmptyHint } from './empty-hint';
import { HomeSection } from './home-section';
import { WorkItemRow } from './work-item-row';
import type { HomeProviderRequestDto } from '../types';

/** "Provider requests I owe" — plan 7 shape, always `[]` until that ships.
 * The Provider variant renders this section first (see `staff-home-body.tsx`). */
export function ProviderRequestsSection({ items }: { items: HomeProviderRequestDto[] }) {
  return (
    <HomeSection title="Provider requests I owe" data-testid="home-provider-requests">
      {items.length === 0 ? (
        <EmptyHint data-testid="home-provider-requests-empty">
          No outstanding provider requests.
        </EmptyHint>
      ) : (
        <ul className="divide-y divide-brand-slate-100">
          {items.map((item) => (
            <WorkItemRow
              key={item.id}
              title={item.studentName}
              subtitle={`Due ${formatDate(item.dueDate)}`}
              href={`/educator/students/${item.studentId}`}
              data-testid={`home-provider-requests-${item.id}`}
            />
          ))}
        </ul>
      )}
    </HomeSection>
  );
}
