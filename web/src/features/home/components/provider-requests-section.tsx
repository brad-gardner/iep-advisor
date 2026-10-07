import { useTranslation } from 'react-i18next';
import { formatDate } from '@/lib/format-date';
import { ListSection } from './list-section';
import { WorkItemRow } from './work-item-row';
import type { HomeProviderRequestDto } from '../types';

/** "Provider requests I owe" — plan 7 shape, always `[]` until that ships.
 * The Provider variant renders this section first (see `staff-home-body.tsx`). */
export function ProviderRequestsSection({ items }: { items: HomeProviderRequestDto[] }) {
  const { t } = useTranslation('home');
  return (
    <ListSection
      title={t('providerRequests.title')}
      data-testid="home-provider-requests"
      items={items}
      emptyHint={t('providerRequests.emptyHint')}
      itemKey={(item) => item.id}
      renderRow={(item) => (
        <WorkItemRow
          title={item.studentName}
          subtitle={t('providerRequests.due', { date: formatDate(item.dueDate) })}
          href={`/educator/students/${item.studentId}`}
          data-testid={`home-provider-requests-${item.id}`}
        />
      )}
    />
  );
}
