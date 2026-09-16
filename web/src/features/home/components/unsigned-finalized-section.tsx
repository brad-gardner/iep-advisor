import { formatDate } from '@/lib/format-date';
import { ListSection } from './list-section';
import { WorkItemRow } from './work-item-row';
import type { HomeUnsignedDto } from '../types';

/** SchoolAdmin/DistrictAdmin "Unsigned finalized documents" — plan 7 shape,
 * always `[]` until finalize-signing ships. Empty-safe by construction. */
export function UnsignedFinalizedSection({ items }: { items: HomeUnsignedDto[] }) {
  return (
    <ListSection
      title="Unsigned finalized documents"
      data-testid="home-unsigned-finalized"
      items={items}
      emptyHint="No finalized documents are waiting on a signature."
      itemKey={(item) => item.versionId}
      renderRow={(item) => (
        <WorkItemRow
          title={item.studentName}
          subtitle={`Finalized ${formatDate(item.finalizedAt)}`}
          href={`/educator/students/${item.studentId}/authored-versions/${item.versionId}`}
          data-testid={`home-unsigned-finalized-${item.versionId}`}
        />
      )}
    />
  );
}
